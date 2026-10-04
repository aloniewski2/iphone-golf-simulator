# Blender launch diagnosis

Blender 5.2.2 LTS reproducibly exited with signal 139 during sandbox startup, even with --factory-startup and a print-only Python expression. The crash occurred before the modeling script.

The Blender backtrace points to supports_barycentric_whitelist → MTLBackend::metal_is_supported → GPU_backend_type_selection_detect → WM_init, with _platform_strstr at the failure. The runtime only offers the Metal backend; OpenGL is unsupported in this build.

The same Blender executable runs outside the sandbox with graphics access: builds, FBX exports and all Cycles renders completed with exit code 0. The final “Blender quit” in those logs is expected background-mode termination, not a crash.

Current workflow uses normal graphics access for Blender invocations. No reinstall or preference reset was needed. Crash record: /var/folders/38/dmjzs7x97qldqhndz__9gldr0000gn/T/blender.crash.txt; successful build/audit logs: work/hero-base/.
