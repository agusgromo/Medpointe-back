# Patients

## Billing responsibility

- FoxPro uses `pat.resp_acct` when it refers to another patient; otherwise, it uses the current patient's account. `pat.other_bp` identifies a separate responsible party stored in `resp` under the same account. `removeRP` clears the flag without deleting `resp`; the row's existence does not prove it is still active.
- A plan's insured person may be someone else; selecting that person copies their information into the plan and does not establish who paid a claim.
- In `medpointe_old`, every patient's `resp_acct` is empty. Unresolved: 4 flagged patients without a `resp` row, 4,450 `resp` rows without a visible relationship, and 176 claims whose responsible party has no patient record. Do not invent relationships or discard rows before resolving them. Use new PK/FK relationships without legacy account identifiers in operational tables.

Source: `C:\inetpub\wwwroot\medpointe\deploy\api.prg` (`getPatInfo`, `addRP`, `removeRP`, `vInsured`); `medpointe_old` counts from 2026-09-24. Validate the `resp_acct` variant on other servers.

## Patient Activity

- The general `pat.note` has no source date; preserve it as an undated note. Translate `pat.com_pref` using `patcodes` type `@` into phone, email, text, portal, postal mail or do not contact.
- Visible documents come from individual DBF files in `C:\Data\pmp\medrec`, their FPT files and attachments in `images` or the configured clinical location; filter by category, restrictions and office access. The `medrec` replica is empty; 716 of 717 DBF files are readable (1,267 rows: 851 with text, 64 with a file and 143 without `MR_NO` but with content). The special `.dbf` file requires separate handling.
- Pending: import and reconcile those documents and files, and complete insurance, claim, payment, contact and communication actions before cutover.
