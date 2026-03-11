from __future__ import annotations

import argparse
import json
from pathlib import Path

from vla_control import (
    ActionAdapter,
    DEFAULT_OPENVLA_ARTIFACT_ROOT,
    DEFAULT_OPENVLA_CAMERA_NAME,
    DEFAULT_OPENVLA_IMAGE_HEIGHT,
    DEFAULT_OPENVLA_IMAGE_QUALITY,
    DEFAULT_OPENVLA_IMAGE_WIDTH,
    DEFAULT_OPENVLA_INSTRUCTION,
    DEFAULT_OPENVLA_ROLLOUT_STEPS,
    DEFAULT_OPENVLA_TIMEOUT_SECONDS,
    DEFAULT_OPENVLA_UNNORM_KEY,
    DummyPolicy,
    EvaluationRunner,
    RolloutConfig,
    UnityClient,
    UnityConfig,
    create_openvla_backend,
    run_openvla_rollout,
    run_openvla_single_step_check,
    run_smoke_test,
)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="VLA control utilities")
    parser.add_argument(
        "command",
        choices=["smoke", "dummy-rollout", "openvla-check", "openvla-rollout"],
        help="Command to run",
    )
    parser.add_argument("--host", help="Unity server host")
    parser.add_argument("--port", type=int, help="Unity server port")
    parser.add_argument("--timeout", type=float, help="HTTP timeout in seconds")
    parser.add_argument(
        "--save-dir", type=Path, help="Directory for smoke-test artifacts"
    )
    parser.add_argument(
        "--instruction", help="Instruction string for the dummy rollout"
    )
    parser.add_argument("--max-steps", type=int, help="Maximum rollout steps")
    parser.add_argument("--openvla-url", help="OpenVLA runtime base URL")
    parser.add_argument("--unnorm-key", help="OpenVLA normalization key")
    parser.add_argument("--camera", help="Camera name to use for rollouts")
    parser.add_argument("--image-width", type=int, help="Requested camera image width")
    parser.add_argument(
        "--image-height", type=int, help="Requested camera image height"
    )
    parser.add_argument(
        "--image-quality", type=int, help="Requested camera JPEG quality"
    )
    return parser


def main() -> None:
    args = build_parser().parse_args()

    config = UnityConfig()
    overrides = {
        key: value
        for key, value in {
            "host": args.host,
            "port": args.port,
            "timeout_seconds": args.timeout,
        }.items()
        if value is not None
    }
    if overrides:
        config = config.model_copy(update=overrides)

    client = UnityClient(config)

    if args.command == "smoke":
        result = run_smoke_test(client, save_dir=args.save_dir)
        print(json.dumps(result.model_dump(mode="json"), indent=2))
        return

    if args.command == "dummy-rollout":
        rollout_config = RolloutConfig()
        rollout_overrides = {
            key: value
            for key, value in {
                "instruction": args.instruction,
                "max_steps": args.max_steps,
                "artifact_root": args.save_dir,
            }.items()
            if value is not None
        }
        if rollout_overrides:
            rollout_config = rollout_config.model_copy(update=rollout_overrides)

        runner = EvaluationRunner(client, DummyPolicy(), ActionAdapter())
        summary = runner.run_rollout(rollout_config)
        print(json.dumps(summary.model_dump(mode="json"), indent=2))
        return

    if args.command == "openvla-check":
        backend = create_openvla_backend(
            base_url=args.openvla_url,
            unnorm_key=args.unnorm_key or DEFAULT_OPENVLA_UNNORM_KEY,
            timeout_seconds=args.timeout or DEFAULT_OPENVLA_TIMEOUT_SECONDS,
        )
        result = run_openvla_single_step_check(
            client,
            backend,
            instruction=args.instruction or DEFAULT_OPENVLA_INSTRUCTION,
            camera_name=args.camera or DEFAULT_OPENVLA_CAMERA_NAME,
            image_width=args.image_width or DEFAULT_OPENVLA_IMAGE_WIDTH,
            image_height=args.image_height or DEFAULT_OPENVLA_IMAGE_HEIGHT,
            image_quality=args.image_quality or DEFAULT_OPENVLA_IMAGE_QUALITY,
            action_adapter=ActionAdapter(),
        )
        print(json.dumps(result.model_dump(mode="json"), indent=2))
        return

    if args.command == "openvla-rollout":
        backend = create_openvla_backend(
            base_url=args.openvla_url,
            unnorm_key=args.unnorm_key or DEFAULT_OPENVLA_UNNORM_KEY,
            timeout_seconds=args.timeout or DEFAULT_OPENVLA_TIMEOUT_SECONDS,
        )
        rollout_config = RolloutConfig(
            instruction=args.instruction or DEFAULT_OPENVLA_INSTRUCTION,
            max_steps=args.max_steps or DEFAULT_OPENVLA_ROLLOUT_STEPS,
            camera_name=args.camera or DEFAULT_OPENVLA_CAMERA_NAME,
            image_width=args.image_width or DEFAULT_OPENVLA_IMAGE_WIDTH,
            image_height=args.image_height or DEFAULT_OPENVLA_IMAGE_HEIGHT,
            image_quality=args.image_quality or DEFAULT_OPENVLA_IMAGE_QUALITY,
            artifact_root=args.save_dir or DEFAULT_OPENVLA_ARTIFACT_ROOT,
            reset_before_rollout=True,
            reset_after_rollout=True,
        )
        summary = run_openvla_rollout(
            client,
            backend,
            rollout_config=rollout_config,
            action_adapter=ActionAdapter(),
        )
        print(json.dumps(summary.model_dump(mode="json"), indent=2))


if __name__ == "__main__":
    main()
