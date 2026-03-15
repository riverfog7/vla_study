from __future__ import annotations

from contextlib import asynccontextmanager

from fastapi import FastAPI, Request  # type: ignore[import-not-found]
from fastapi.responses import JSONResponse  # type: ignore[import-not-found]

from octo_runtime.config import OctoRuntimeConfig
from octo_runtime.errors import (
    InvalidDatasetStatisticsKeyError,
    ModelLoadError,
    OctoRuntimeError,
    RuntimeNotReadyError,
    RuntimeRequestError,
)
from octo_runtime.models import (
    ErrorResponse,
    HealthResponse,
    PredictRequest,
    PredictResponse,
    ReadyResponse,
)
from octo_runtime.runtime import OctoModelRuntime


def create_app() -> FastAPI:
    config = OctoRuntimeConfig()
    runtime = OctoModelRuntime(config)

    @asynccontextmanager
    async def lifespan(_: FastAPI):
        if config.load_on_startup:
            try:
                runtime.load()
            except ModelLoadError:
                pass
        yield

    app = FastAPI(title=config.app_name, version=config.app_version, lifespan=lifespan)
    app.state.runtime = runtime
    app.state.runtime_config = config

    @app.exception_handler(RuntimeNotReadyError)
    async def handle_not_ready(_: Request, exc: RuntimeNotReadyError) -> JSONResponse:
        return JSONResponse(
            status_code=503,
            content=ErrorResponse(error="not_ready", details=str(exc)).model_dump(
                mode="json"
            ),
        )

    @app.exception_handler(InvalidDatasetStatisticsKeyError)
    async def handle_invalid_key(
        _: Request, exc: InvalidDatasetStatisticsKeyError
    ) -> JSONResponse:
        return JSONResponse(
            status_code=400,
            content=ErrorResponse(
                error="invalid_dataset_statistics_key", details=str(exc)
            ).model_dump(mode="json"),
        )

    @app.exception_handler(RuntimeRequestError)
    async def handle_bad_request(_: Request, exc: RuntimeRequestError) -> JSONResponse:
        return JSONResponse(
            status_code=400,
            content=ErrorResponse(error="invalid_request", details=str(exc)).model_dump(
                mode="json"
            ),
        )

    @app.exception_handler(OctoRuntimeError)
    async def handle_runtime_error(_: Request, exc: OctoRuntimeError) -> JSONResponse:
        return JSONResponse(
            status_code=500,
            content=ErrorResponse(error="runtime_error", details=str(exc)).model_dump(
                mode="json"
            ),
        )

    @app.get("/health", response_model=HealthResponse)
    async def health() -> HealthResponse:
        return HealthResponse(
            ok=True,
            app=config.app_name,
            version=config.app_version,
            model_id=config.model_id,
        )

    @app.get("/ready", response_model=ReadyResponse)
    async def ready() -> ReadyResponse:
        return runtime.get_ready_response()

    @app.post("/predict", response_model=PredictResponse)
    async def predict(request: PredictRequest) -> PredictResponse:
        return runtime.predict(request)

    return app


def main() -> None:
    import uvicorn  # type: ignore[import-not-found]

    config = OctoRuntimeConfig()
    uvicorn.run(
        create_app(),
        host=config.host,
        port=config.port,
        log_level=config.log_level,
        loop="asyncio",
    )
