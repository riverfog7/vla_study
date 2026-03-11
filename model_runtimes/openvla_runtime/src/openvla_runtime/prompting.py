from __future__ import annotations


def build_openvla_prompt(instruction: str) -> str:
    normalized_instruction = instruction.strip()
    if not normalized_instruction:
        raise ValueError("Instruction must be non-empty.")

    return f"In: What action should the robot take to {normalized_instruction}?\nOut:"
