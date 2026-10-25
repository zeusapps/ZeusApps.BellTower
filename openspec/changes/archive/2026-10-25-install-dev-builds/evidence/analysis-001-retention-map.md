<!-- Saved verbatim by the orchestrator under the client-refusal fallback; content is the code-analyzer's (Analysis 001). -->

Retire against `UpdateTransaction.Cleanup` at the reviewed identity. Cleanup runs in every packaged tray since `ed16b9f`/`v0.1.8`: `TrayApplication.cs:62-63` starts `UpdateDiscovery.RunAsync` for payload processes, and `UpdateDiscovery.cs:60-68` runs Cleanup at startup and every 15 minutes. It also runs after each `Apply`.

| Rule | Cleanup (`UpdateTransaction.cs`) | Retire (`install-dev.ps1`) |
| --- | --- | --- |
| Gates | update, then launch (189-190) | update, then launch (199-201) |
| Selected | retained (192) | refused (204) |
| previous | retained (193-194) | refused (205) |
| Recovery plans (outcome missing, failed, rolled-back or staged → Original and Target) | retained (196-211) | refused (212-224) |
| Valid contract | `Validate()` (220-221) | `Contract()` through `Payload()` validates (206) |
| Exclusive lease | (222) | (225) |
| Unowned files | skip (225-227) | refuse (229-233) |
| VerifyFiles | (228) | (234) |
| Delete | `Directory.Delete` (231) | `Remove-Item` (237) |
| Extra | none: stable and dev alike | dev marker or range, or `-Legacy` plus "newer than the effective version" (207-211) |
