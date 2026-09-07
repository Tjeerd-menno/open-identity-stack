# Unrestricted administrative approval

The literal role name `admin` grants no permission. An explicitly assigned `*` remains the all-permissions grant for current and future platform permissions. Use concrete permissions for routine operators and reserve unrestricted access for controlled administration and independently accessible emergency recovery.

## Protected operations

Creating or expanding a role to include `*`, enabling such a role, assigning it to a user, mapping it to a group, adding a member to that group, re-enabling an unrestricted user, or resetting that user's password requires all of the following:

- An active, existing human user whose current persisted effective roles explicitly include `*`.
- Actual human authentication within five minutes. A renewed cookie or refreshed token does not reset this interval.
- Acknowledgement of unrestricted access. Existing role permission requests support `acknowledgeWildcardGrant: true`; other protected operations accept `X-OIS-Administrative-Approval: acknowledge`.
- A durable approval-intent audit record before any mutation. Success is recorded separately after persistence.

Group role mappings support the existing role-name representation and the role IDs sent by Management Web. Groups cannot inject subject, issuer, audience, role, scope, permission, authentication-time, verification, persisted identity/profile fields (including phone number), or internal protocol claims. Previously stored reserved claim mappings are suppressed during new claim projection.

A machine credential cannot approve a human operation. A stale token carrying `*` cannot approve after its user's persisted unrestricted authority is removed. Ordinary concrete permission changes continue to use their existing endpoint permissions.

## Operator workflow

Management Web requests acknowledgement for the pending operation. Cancellation sends no approved retry. When authentication is too old or its time is unknown, the dialog starts sign-in with `prompt=login` and `max_age=0`, returns to the operator's page, and requires the operator to repeat and review the operation. It does not automatically replay a mutation across sign-in.

Local password sign-in supplies the actual authentication event. Federation can supply freshness only from the upstream's validated `auth_time` claim. Receiving an SSO callback is insufficient; providers without trustworthy authentication time require an independent supported sign-in method for protected approval.

The API returns HTTP 403 Problem Details for approval failures, with an `errorCode`:

| Suffix | Operator action |
| --- | --- |
| `HumanRequired` | Use a human administrative session. |
| `AuthorityRequired` | Use an active human who currently holds the explicit all-permissions grant. |
| `ReauthenticationRequired` | Sign in again through a method that establishes actual authentication time. |
| `AcknowledgementRequired` | Review and explicitly acknowledge unrestricted access. |

Authority is captured before the operation reads users, roles, or groups. Changes to these authority dependencies and the protected mutation share a database revision fence: concurrent changes reject the stale operation with HTTP 409 and roll back its mutation. Reload and review the operation before retrying; approval is never automatically replayed. Ordinary login timestamp updates do not invalidate this snapshot.

Codes have the prefix `Forbidden.AdministrativeApproval.`. The acknowledgement applies to one request; it does not establish authentication or authority.

## Audit and rollout

Approval audit writes use an independent persistence scope so that logging cannot accidentally save pending role or group changes. `AdministrativeApproval.IntentApproved` is an approved intention, not proof of a completed grant. `MutationSucceeded` records successful persistence. `MutationNotConfirmed` means no successful completion was recorded; inspect the current state before retrying. Intent audit storage failure prevents approval. Outcome audit failure preserves the operation's result, emits a critical diagnostic without identities or exception details, and leaves the outcome pending for a request-end retry. If that retry also fails, reconcile the durable approved intent against persisted state; do not repeat the mutation merely to repair its audit trail.

Preserve a tested independently accessible emergency administrator before rollout. Initial bootstrap establishes explicit permissions through the controlled deployment process; routine seed reruns must not restore removed grants, activate disabled accounts, or manufacture verification evidence. This change does not create a bootstrap or recovery bypass.

Deploy with the credential cutover in the [boundary decision](../adr/0005-identity-and-administrative-trust-boundaries.md): invalidate pre-cutover sessions and grants and require fresh authentication. Older credentials may contain previously permitted group-injected claims and cannot become trustworthy merely because claim mapping is now restricted. Downgrading to a version that infers authority from role names or permits ungoverned grants reopens those boundaries.
