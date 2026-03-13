using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VlaStudy.UnityHarness.Camera;
using VlaStudy.UnityHarness.Data;
using VlaStudy.UnityHarness.Robot;
using VlaStudy.UnityHarness.Simulation;

namespace VlaStudy.UnityHarness.Api
{
    public class HttpApiServer : MonoBehaviour
    {
        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] private int port = 8080;
        [SerializeField] private string appName = "unity-vla-sim";
        [SerializeField] private string version = "0.1.0";

        public string Host => host;
        public int Port => port;

        private HttpListener _listener;
        private CancellationTokenSource _cancellationTokenSource;
        private MainThreadDispatcher _mainThreadDispatcher;
        private SimulationController _simulationController;
        private SceneStateService _sceneStateService;
        private CameraCaptureService _cameraCaptureService;
        private CameraRegistry _cameraRegistry;
        private RuntimeCameraService _runtimeCameraService;
        private IRobotAdapter _robotAdapter;
        private TaskResetService _taskResetService;

        public void Configure(
            MainThreadDispatcher mainThreadDispatcher,
            SimulationController simulationController,
            SceneStateService sceneStateService,
            CameraCaptureService cameraCaptureService,
            CameraRegistry cameraRegistry,
            RuntimeCameraService runtimeCameraService,
            IRobotAdapter robotAdapter,
            TaskResetService taskResetService)
        {
            _mainThreadDispatcher = mainThreadDispatcher;
            _simulationController = simulationController;
            _sceneStateService = sceneStateService;
            _cameraCaptureService = cameraCaptureService;
            _cameraRegistry = cameraRegistry;
            _runtimeCameraService = runtimeCameraService;
            _robotAdapter = robotAdapter;
            _taskResetService = taskResetService;
        }

        private void Start()
        {
            StartServer();
        }

        private void OnDestroy()
        {
            StopServer();
        }

        private void OnApplicationQuit()
        {
            StopServer();
        }

        private void StartServer()
        {
            if (_listener != null)
            {
                return;
            }

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://{host}:{port}/");
            if (string.Equals(host, "127.0.0.1", StringComparison.Ordinal))
            {
                _listener.Prefixes.Add($"http://localhost:{port}/");
            }

            _listener.IgnoreWriteExceptions = true;
            _listener.Start();

            _cancellationTokenSource = new CancellationTokenSource();
            _ = ListenLoopAsync(_cancellationTokenSource.Token);
            Debug.Log($"HTTP API listening on http://{host}:{port}/v1/");
        }

        private void StopServer()
        {
            if (_listener == null)
            {
                return;
            }

            try
            {
                _cancellationTokenSource?.Cancel();
                _listener.Stop();
                _listener.Close();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to stop HTTP API listener cleanly: {exception.Message}");
            }
            finally
            {
                _listener = null;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception exception) when (exception is ObjectDisposedException || exception is HttpListenerException)
                {
                    break;
                }

                _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var path = NormalizePath(request.Url?.AbsolutePath ?? string.Empty);

                switch (request.HttpMethod)
                {
                    case "GET" when path == "/v1/health":
                        await WriteJsonAsync(context.Response, HttpStatusCode.OK, new HealthResponse
                        {
                            ok = true,
                            app = appName,
                            version = version,
                        });
                        return;

                    case "GET" when path == "/v1/state":
                    {
                        var state = await _mainThreadDispatcher.EnqueueAsync(() => _sceneStateService.BuildStateResponse());
                        await WriteJsonAsync(context.Response, HttpStatusCode.OK, state);
                        return;
                    }

                    case "GET" when path.StartsWith("/v1/camera/", StringComparison.Ordinal) && path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase):
                    {
                        var cameraName = ExtractCameraName(path);
                        var width = ParseInt(request.QueryString["width"], 256);
                        var height = ParseInt(request.QueryString["height"], 256);
                        var quality = ParseInt(request.QueryString["quality"], 80);

                        var imageBytes = await _mainThreadDispatcher.EnqueueAsync(() => _cameraCaptureService.CaptureJpeg(cameraName, width, height, quality));
                        await WriteBytesAsync(context.Response, HttpStatusCode.OK, "image/jpeg", imageBytes);
                        return;
                    }

                    case "GET" when path == "/v1/cameras":
                    {
                        var response = await _mainThreadDispatcher.EnqueueAsync(() => _cameraRegistry.BuildCameraListResponse());
                        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response);
                        return;
                    }

                    case "POST" when path == "/v1/sim/step":
                    {
                        var stepRequest = await ReadJsonBodyAsync<StepRequest>(request);
                        if (stepRequest.steps <= 0)
                        {
                            await WriteErrorAsync(context.Response, HttpStatusCode.BadRequest, "invalid_request", "steps must be greater than zero.");
                            return;
                        }

                        var stepResult = await _mainThreadDispatcher.EnqueueAsync(() => _simulationController.StepSimulation(stepRequest.steps, stepRequest.dt));
                        await WriteJsonAsync(context.Response, HttpStatusCode.OK, new StepResponse
                        {
                            ok = true,
                            sim_time = stepResult.SimTime,
                            step_count = stepResult.StepCount,
                        });
                        return;
                    }

                    case "POST" when path == "/v1/robot/move_to_pose":
                    {
                        var command = await ReadJsonBodyAsync<PoseCommand>(request);
                        if (command == null || !command.IsValid())
                        {
                            await WriteErrorAsync(context.Response, HttpStatusCode.BadRequest, "invalid_request", "Pose command is missing required fields.");
                            return;
                        }

                        var commandId = await _mainThreadDispatcher.EnqueueAsync(() => _robotAdapter.ApplyPoseCommand(command));
                        await WriteJsonAsync(context.Response, HttpStatusCode.OK, new MoveToPoseResponse
                        {
                            accepted = true,
                            command_id = commandId,
                        });
                        return;
                    }

                    case "POST" when path == "/v1/cameras/upsert":
                    {
                        var upsertRequest = await ReadJsonBodyAsync<UpsertCameraRequest>(request);
                        var response = await _mainThreadDispatcher.EnqueueAsync(() => _runtimeCameraService.UpsertCamera(upsertRequest));
                        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response);
                        return;
                    }

                    case "POST" when path == "/v1/reset":
                        await _mainThreadDispatcher.EnqueueAsync(() => _taskResetService.ResetScene());
                        await WriteJsonAsync(context.Response, HttpStatusCode.OK, new ResetResponse { ok = true });
                        return;

                    case "DELETE" when path.StartsWith("/v1/cameras/", StringComparison.Ordinal):
                    {
                        var cameraName = ExtractManagedCameraName(path);
                        var response = await _mainThreadDispatcher.EnqueueAsync(() => _runtimeCameraService.DeleteRuntimeCamera(cameraName));
                        await WriteJsonAsync(context.Response, HttpStatusCode.OK, response);
                        return;
                    }
                }

                await WriteErrorAsync(context.Response, HttpStatusCode.NotFound, "not_found", $"No route matches {request.HttpMethod} {path}.");
            }
            catch (ArgumentException exception)
            {
                await WriteErrorAsync(context.Response, HttpStatusCode.BadRequest, "invalid_request", exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                await WriteErrorAsync(context.Response, HttpStatusCode.ServiceUnavailable, "service_unavailable", exception.Message);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                await WriteErrorAsync(context.Response, HttpStatusCode.InternalServerError, "internal_error", exception.Message);
            }
            finally
            {
                context.Response.OutputStream.Close();
            }
        }

        private static async Task<T> ReadJsonBodyAsync<T>(HttpListenerRequest request) where T : class
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body))
            {
                throw new ArgumentException("Request body is required.");
            }

            var parsed = JsonUtility.FromJson<T>(body);
            if (parsed == null)
            {
                throw new ArgumentException("Request body could not be parsed as JSON.");
            }

            return parsed;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "/";
            }

            if (path.Length > 1 && path.EndsWith("/", StringComparison.Ordinal))
            {
                return path.TrimEnd('/');
            }

            return path;
        }

        private static string ExtractCameraName(string path)
        {
            var prefixLength = "/v1/camera/".Length;
            var suffixLength = ".jpg".Length;
            return Uri.UnescapeDataString(path.Substring(prefixLength, path.Length - prefixLength - suffixLength));
        }

        private static string ExtractManagedCameraName(string path)
        {
            var prefixLength = "/v1/cameras/".Length;
            return Uri.UnescapeDataString(path.Substring(prefixLength));
        }

        private static int ParseInt(string rawValue, int fallback)
        {
            return int.TryParse(rawValue, out var parsedValue) ? parsedValue : fallback;
        }

        private static async Task WriteJsonAsync(HttpListenerResponse response, HttpStatusCode statusCode, object payload)
        {
            var json = JsonUtility.ToJson(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            await WriteBytesAsync(response, statusCode, "application/json", bytes);
        }

        private static Task WriteErrorAsync(HttpListenerResponse response, HttpStatusCode statusCode, string error, string details)
        {
            return WriteJsonAsync(response, statusCode, new ErrorResponse
            {
                error = error,
                details = details,
            });
        }

        private static async Task WriteBytesAsync(HttpListenerResponse response, HttpStatusCode statusCode, string contentType, byte[] bytes)
        {
            response.StatusCode = (int)statusCode;
            response.ContentType = contentType;
            response.ContentLength64 = bytes.LongLength;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }

    }
}
