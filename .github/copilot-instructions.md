# Copilot Instructions

## Project Guidelines
- When stress-testing against a local Podman/Docker-hosted Oracle Free database, raise the `processes` init parameter (ALTER SYSTEM SET processes=<N> SCOPE=SPFILE + container restart) before high-concurrency runs, since the default (~300) is easily exhausted and causes ORA-00020, which manifests as misleading ORA-12541/ORA-50201 'no listener' errors.
- On this machine, Podman Desktop's WSL2 VM has broken cgroup v2 delegation (pids controller not delegated to non-systemd/user.slice or machine.slice, and the root non-systemd cgroup has processes attached directly, blocking delegation). Workaround: start containers with `--cgroups=disabled` (e.g. `podman run --cgroups=disabled ...`) to avoid `crun: controller 'pids' is not available` errors.