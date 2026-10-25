# QA 004 maintainer Preflight evidence

- Command: `./scripts/install-dev.ps1 -Action Preflight -Package artifacts/dev-builds/bell-tower-0.1.9003-win-x64.zip`
- Result: passed; candidate 0.1.9003; original selection 0.1.10; script reported `mutation: none` except its private operation workspace under repository artifacts.
- The operation manifest records original selection and launcher hashes. After Preflight, current selection hash equals the recorded hash: 
True
. Launcher hash equals the recorded hash: 
True
.
- Verified original payload files still matching their recorded hashes: 
24
/
24
; mismatches: 
0
.
- Tray PID/start-time identity remains the same process: 
True
. No tray stop, restart, setup apply, or provider-session action was performed.
- No `setup --apply` or provider settings were touched. This proves the read-only Preflight path on this machine, not live hook migration or user-machine timeout causality.
