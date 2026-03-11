from __future__ import annotations

import argparse
import json
from pathlib import Path

from vla_control import ActionAdapter, DummyPolicy, EvaluationRunner, RolloutConfig, UnityClient, UnityConfig, run_smoke_test


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="VLA control utilities")
    parser.add_argument("command", choices=["smoke", "dummy-rollout"], help="Command to run")
    parser.add_argument("--host", help="Unity server host")
    parser.add_argument("--port", type=int, help="Unity server port")
    parser.add_argument("--timeout", type=float, help="HTTP timeout in seconds")
    parser.add_argument("--save-dir", type=Path, help="Directory for smoke-test artifacts")
    parser.add_argument("--instruction", help="Instruction string for the dummy rollout")
    parser.add_argument("--max-steps", type=int, help="Maximum rollout steps")
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


if __name__ == "__main__":
    main()
