# Native adoption probe follow-up 006

Date: 2026-10-29. Scope: prepare and exercise a private helper that obtains the current elevated process's linked standard token and attempts to create one harmless same-user lower-privilege child. No Codex/Claude provider was launched. All helper sources and run outputs are ignored artifacts; no product source, provider configuration, user process, account, credential, or host security setting was changed.

## Current API documentation

The required Context7 lookup resolved the Microsoft Learn Win32 API library, but its documentation query returned only generic excerpts. I therefore checked the current official Microsoft Learn references directly: [`GetTokenInformation`](https://learn.microsoft.com/en-us/windows/win32/api/securitybaseapi/nf-securitybaseapi-gettokeninformation), [`TOKEN_INFORMATION_CLASS`](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ne-winnt-token_information_class), [`TOKEN_LINKED_TOKEN`](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-token_linked_token), [`EqualSid`](https://learn.microsoft.com/en-us/windows/win32/api/securitybaseapi/nf-securitybaseapi-equalsid), [`DuplicateTokenEx`](https://learn.microsoft.com/en-us/windows/win32/api/securitybaseapi/nf-securitybaseapi-duplicatetokenex), and [`CreateProcessWithTokenW`](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createprocesswithtokenw).

The docs specify that `TokenLinkedToken` returns a handle to the linked token; `EqualSid` tests SID equality; `CreateProcessWithTokenW` requires a primary token with `TOKEN_QUERY`, `TOKEN_DUPLICATE`, and `TOKEN_ASSIGN_PRIMARY`; and `DuplicateTokenEx` can create a primary token from an impersonation token. The calling privilege requirement for `CreateProcessWithTokenW` is `SeImpersonatePrivilege`. The helper used only the current process token and its linked-token handle, and it never printed or retained either SID.

## Helper and checks

The ignored helper is under `artifacts/native-adoption-003/token-link-launcher`. It opens the current process token, reads its `TokenLinkedToken`, compares the two `TokenUser` SIDs with `EqualSid`, reads elevation/type/impersonation-level booleans and integers, and records no SID or account name. Its bounded smoke child would be the helper executable itself, launched hidden with no file writes in the Windows system directory. The parent would query the child's token and record exact PID, creation FILETIME, executable path, same-user equality, elevation, and natural exit; timeout cleanup is identity-checked.

The helper and its project built successfully with the installed .NET 10 SDK. Read-only token inspection returned:

| Token | Type | Elevation | Impersonation level | Same-user SID check |
| --- | ---: | --- | ---: | --- |
| Current process token | Primary (1) | Elevated | Not applicable | Baseline |
| `TokenLinkedToken` handle | Impersonation (2) | Not elevated | SecurityIdentification (1) | Equal to current token |

The same-user comparison passed without emitting either SID. No child process was created, so there is no child PID, creation identity, image, or `TokenElevation=false` result to report.

## Exact API failure

`CreateProcessWithTokenW` cannot consume the linked token directly because Windows returned it as an impersonation token. `DuplicateTokenEx` was tried with the minimum required token access rights and two primary-token duplicate impersonation-level settings: preserving the linked token's SecurityIdentification level (1), then the documented conversion path's SecurityImpersonation level (2). Both attempts failed in `DuplicateTokenEx` with Win32 error 1346: “Either a required impersonation level was not provided, or the provided impersonation level is invalid.” The helper also opened the source token with `TOKEN_QUERY | TOKEN_DUPLICATE | TOKEN_ASSIGN_PRIMARY`; the same error remained. Neither the source elevated token nor linked token was modified.

An earlier harness iteration reported `ERROR_DIRECTORY` (267) while using an artifact directory as the child's working directory. That was corrected to a no-write child with the Windows system directory as its working directory. The current blocker is the linked token's impersonation-level/primary-token conversion, before process creation. No alternate token source, `CreateProcessAsUserW` workaround, credentials, user process, UAC prompt, or security-mode change was attempted.

## Disposition

The helper proves that the current elevated token has a same-user linked token with `TokenElevation=false`, but this environment exposes that linked token only as a SecurityIdentification impersonation token. The documented duplicate-to-primary operation fails with error 1346 even when its impersonation level is explicitly set to SecurityImpersonation. The bounded smoke therefore did not launch a child and did not establish a usable lower-privilege launcher. Shared-mode provider probes remain unrun. This is a concrete Windows token/API prerequisite failure for this execution context, not evidence of a Codex provider restriction.
