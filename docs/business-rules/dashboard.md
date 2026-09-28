# Dashboard

- Filter the schedule by date and office. FoxPro excludes appointments without a patient or with status `X` (voided); retain canceled `XC`, no-show `XN` and rescheduled `XR` as distinct statuses.
- The greeting uses the schedule provider; `user.prv` defines the default assignment. Office lists the `config` catalog and restores the browser selection or `user.curr_off`/`user.office`; retain special appointment references without listing them as active offices.
- “Notes Pending” counts appointments with a closed encounter and an unsigned note. The third metric counts documents in the user's inbox, not open claims.
- The inbox considers user, role, group and shared mailboxes; exclude documents without a patient or not yet due. Date and office selections are interface state, not clinical data.
- Clicking an appointment opens its clinical chart when the module is available; the `DB1` right-click context menu remains pending in React.
- Pending: migrate `doc` and its recipients before displaying an inbox count. The local replica has 92 `doc` rows; 36 recipients do not match current users, roles or groups.

Sources: `C:\inetpub\wwwroot\medpointe\deploy\api.prg` (`getDashBoard`), `C:\Data\pmp\history\clinical.prg` (`PopulateInbox`) and `GENERAL.PRG` (`AptStatusCode`, `GetUserList`).
