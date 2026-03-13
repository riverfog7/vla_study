# wx250s URDF Assets

This directory vendors the Interbotix `wx250s` description assets needed for Unity URDF import.

- `wx250s.urdf` is a static URDF expanded from the upstream `wx250s.urdf.xacro` defaults.
- `meshes/` contains the upstream visual and collision meshes used by the robot description.

Recommended Unity workflow:

1. Install `com.unity.robotics.urdf-importer` from `unity/Packages/manifest.json`.
2. In the Unity editor, right-click `unity/Assets/URDF/wx250s/wx250s.urdf` and import the robot.
3. Place the imported robot in `Main.unity`.
4. On `VlaHarnessScene`, use `ArticulatedRobotAdapter` to auto-bind the articulation hierarchy.
5. Run `Tools/VLA/Bind Scene References From Articulated Adapter` to copy the authored base, end-effector, and wrist camera transforms into `HarnessSceneReferences`.

Upstream source:

- Interbotix `interbotix_xsarm_descriptions`
- Unity URDF Importer `v0.5.2`
