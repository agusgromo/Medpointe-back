-- 1.INSERT DATA INTO medpoint_old before this point
-- 2.Run this in the new database after creating the new schema.

-- ROLLBACK;

begin;

create extension if not exists dblink;

create function pg_temp.legacy_conn()
returns text
language sql
as $$
  select 'host=localhost port=5432 dbname=medpointe_old user=postgres password=1234';
$$;

create function pg_temp.blank_to_null(value text)
returns text
language sql
immutable
as $$
  select nullif(btrim(value), '');
$$;

create function pg_temp.legacy_sex(value text)
returns text
language sql
immutable
as $$
  select case upper(coalesce(btrim(value), ''))
    when 'M' then 'male'
    when 'F' then 'female'
    else 'unknown'
  end;
$$;

create function pg_temp.numeric_or_null(value text)
returns numeric
language sql
immutable
as $$
  select case
    when btrim(coalesce(value, '')) ~ '^-?[0-9]+(\.[0-9]+)?$' then btrim(value)::numeric
    else null
  end;
$$;

create function pg_temp.first_numeric_or_null(value text)
returns numeric
language sql
immutable
as $$
  select case
    when regexp_replace(replace(coalesce(value, ''), ',', '.'), '^.*?(-?[0-9]+(\.[0-9]+)?).*$','\1') ~ '^-?[0-9]+(\.[0-9]+)?$'
      then regexp_replace(replace(coalesce(value, ''), ',', '.'), '^.*?(-?[0-9]+(\.[0-9]+)?).*$','\1')::numeric
    else null
  end;
$$;

create function pg_temp.bounded_numeric_or_null(value text, min_value numeric, max_value numeric)
returns numeric
language sql
immutable
as $$
  select case
    when pg_temp.first_numeric_or_null(value) between min_value and max_value
      then pg_temp.first_numeric_or_null(value)
    else null
  end;
$$;

create function pg_temp.legacy_systolic_bp(value text)
returns numeric
language sql
immutable
as $$
  select coalesce(
    pg_temp.bounded_numeric_or_null(value, 40, 300),
    case
      when btrim(coalesce(value, '')) ~ '^[0-9]{5}$'
        and substring(btrim(value) from 1 for 3)::numeric between 40 and 300
      then substring(btrim(value) from 1 for 3)::numeric
      else null
    end
  );
$$;

create function pg_temp.legacy_diastolic_bp(systolic_value text, diastolic_value text)
returns numeric
language sql
immutable
as $$
  select coalesce(
    pg_temp.bounded_numeric_or_null(diastolic_value, 20, 200),
    case
      when btrim(coalesce(systolic_value, '')) ~ '^[0-9]{5}$'
        and substring(btrim(systolic_value) from 4 for 2)::numeric between 20 and 200
      then substring(btrim(systolic_value) from 4 for 2)::numeric
      when btrim(coalesce(diastolic_value, '')) ~ '^[0-9]{5}$'
        and substring(btrim(diastolic_value) from 4 for 2)::numeric between 20 and 200
      then substring(btrim(diastolic_value) from 4 for 2)::numeric
      else null
    end
  );
$$;

create function pg_temp.legacy_temperature_c(value text)
returns numeric
language sql
immutable
as $$
  select case
    when pg_temp.first_numeric_or_null(value) between 80 and 115
      then round(((pg_temp.first_numeric_or_null(value) - 32) * 5 / 9)::numeric, 2)
    when pg_temp.first_numeric_or_null(value) between 30 and 45
      then round(pg_temp.first_numeric_or_null(value), 2)
    else null
  end;
$$;

create function pg_temp.legacy_height_cm(value text)
returns numeric
language sql
immutable
as $$
  select case
    when pg_temp.first_numeric_or_null(value) between 20 and 100
      then round((pg_temp.first_numeric_or_null(value) * 2.54)::numeric, 2)
    when pg_temp.first_numeric_or_null(value) between 100 and 260
      then round(pg_temp.first_numeric_or_null(value), 2)
    else null
  end;
$$;

create function pg_temp.legacy_weight_kg(value text)
returns numeric
language sql
immutable
as $$
  select case
    when pg_temp.first_numeric_or_null(value) between 2 and 1000
      then round((pg_temp.first_numeric_or_null(value) / 2.20462)::numeric, 2)
    else null
  end;
$$;

create function pg_temp.legacy_pain_score(value text)
returns smallint
language sql
immutable
as $$
  select case
    when pg_temp.first_numeric_or_null(value) between 0 and 10
      then pg_temp.first_numeric_or_null(value)::smallint
    else null
  end;
$$;

create function pg_temp.legacy_timestamptz(day_value date, hhmm numeric)
returns timestamptz
language plpgsql
immutable
as $$
declare
  time_value integer := coalesce(hhmm, 0)::integer;
  hour_value integer;
  minute_value integer;
begin
  if day_value is null then
    return null;
  end if;

  hour_value := greatest(0, least(23, time_value / 100));
  minute_value := greatest(0, least(59, time_value % 100));

  return (day_value + make_time(hour_value, minute_value, 0)) at time zone 'America/Los_Angeles';
end;
$$;

create function pg_temp.appointment_status(
  legacy_status text,
  arrived boolean,
  triaged boolean,
  checkedout boolean,
  complete boolean
)
returns text
language sql
immutable
as $$
  select case
    when upper(coalesce(legacy_status, '')) = 'XC' then 'cancelled'
    when upper(coalesce(legacy_status, '')) = 'XN' then 'no_show'
    when coalesce(complete, false) then 'completed'
    when coalesce(checkedout, false) then 'checked_out'
    when coalesce(triaged, false) then 'triage'
    when coalesce(arrived, false) then 'checked_in'
    else 'scheduled'
  end;
$$;

create function pg_temp.billing_claim_status(
  legacy_status text,
  total_charge numeric,
  total_paid numeric,
  total_adjustment numeric,
  insurance_balance numeric,
  patient_balance numeric,
  submitted boolean
)
returns text
language sql
immutable
as $$
  select case
    when upper(coalesce(legacy_status, '')) in ('V', 'X') then 'voided'
    when coalesce(total_charge, 0) > 0
      and coalesce(insurance_balance, 0) = 0
      and coalesce(patient_balance, 0) = 0
      and (coalesce(total_paid, 0) > 0 or coalesce(total_adjustment, 0) > 0) then 'paid'
    when coalesce(submitted, false) then 'submitted'
    when coalesce(total_charge, 0) > 0 then 'ready_to_bill'
    else 'draft'
  end;
$$;

create function pg_temp.billing_stage(
  legacy_bill_stage text,
  incomplete boolean,
  claim_status text,
  submitted boolean
)
returns text
language sql
immutable
as $$
  select case
    when claim_status in ('paid', 'voided') then 'closed'
    when coalesce(incomplete, false) then 'coding_review'
    when pg_temp.blank_to_null(legacy_bill_stage) is not null then 'follow_up'
    when coalesce(submitted, false) then 'submitted'
    when claim_status = 'ready_to_bill' then 'ready_to_bill'
    else 'charge_entry'
  end;
$$;

create temp table _provider_map (
  legacy_code text primary key,
  provider_id bigint not null
) on commit drop;

create temp table _location_map (
  legacy_code text primary key,
  location_id bigint not null
) on commit drop;

create temp table _room_map (
  legacy_office text not null,
  legacy_room text not null,
  room_id bigint not null,
  primary key (legacy_office, legacy_room)
) on commit drop;

create temp table _appointment_type_map (
  legacy_code text primary key,
  appointment_type_id bigint not null
) on commit drop;

create temp table _patient_map (
  legacy_acct text not null,
  legacy_dep_no text not null,
  patient_id bigint not null,
  primary key (legacy_acct, legacy_dep_no)
) on commit drop;

create temp table _carrier_map (
  legacy_code text primary key,
  carrier_id bigint not null
) on commit drop;

create temp table _pharmacy_map (
  legacy_code text primary key,
  pharmacy_id bigint not null
) on commit drop;

create temp table _appointment_map (
  legacy_visit_no text primary key,
  appointment_id bigint not null
) on commit drop;

create temp table _visit_map (
  legacy_visit_no text primary key,
  visit_id bigint not null
) on commit drop;

create temp table _policy_map (
  legacy_acct text not null,
  legacy_dep_no text not null,
  legacy_carrier_code text,
  legacy_plan_no text,
  priority smallint not null,
  policy_id bigint not null
) on commit drop;

create temp table _claim_map (
  legacy_claim_no text primary key,
  claim_id bigint not null
) on commit drop;

create temp table _old_users as
select *
from dblink(
  pg_temp.legacy_conn(),
  'select "name", "password" from "user" where coalesce("inactive", false) = false'
) as t(username text, password text);

create temp table _old_prv as
select *
from dblink(
  pg_temp.legacy_conn(),
  'select "code", "desc", "first", "last", "title", "prv_type", "inactive", "office", "room"
   from "prv"'
) as t(
  code text,
  description text,
  first_name text,
  last_name text,
  title text,
  provider_type text,
  inactive boolean,
  office text,
  room text
);

create temp table _old_pharm as
select *
from dblink(
  pg_temp.legacy_conn(),
  'select "code", "name", "address1", "address2", "city", "state", "zip",
          "phone", "fax_no", "area", "id", "inactive"
   from "pharm"'
) as t(
  code text,
  name text,
  address_line1 text,
  address_line2 text,
  city text,
  state text,
  postal_code text,
  phone text,
  fax_number text,
  area text,
  external_identifier text,
  inactive boolean
);

create temp table _old_languages as
select *
from dblink(
  pg_temp.legacy_conn(),
  'select "code", "desc", "misc"
   from "patcodes"
   where "type" = ''l'''
) as t(
  old_language_code text,
  description text,
  language_code text
);

create temp table _language_map (
  old_language_code text primary key,
  language_id bigint not null
) on commit drop;

with source_languages as (
  select
    coalesce(
      nullif(trim(both '-' from regexp_replace(lower(min(pg_temp.blank_to_null(language_code))), '[^a-z0-9]+', '-', 'g')), ''),
      trim(both '-' from regexp_replace(lower(pg_temp.blank_to_null(description)), '[^a-z0-9]+', '-', 'g'))
    ) as code,
    pg_temp.blank_to_null(description) as name,
    min(pg_temp.blank_to_null(language_code)) as hl7_code
  from _old_languages
  where pg_temp.blank_to_null(old_language_code) is not null
    and pg_temp.blank_to_null(description) is not null
  group by pg_temp.blank_to_null(description)
)
insert into languages (code, name, hl7_code, active)
select code, name, hl7_code, true
from source_languages
on conflict (name) do update set
  name = excluded.name,
  hl7_code = excluded.hl7_code,
  active = true,
  updated_at = now();

insert into _language_map (old_language_code, language_id)
select
  pg_temp.blank_to_null(old_language.old_language_code),
  lang.id
from _old_languages old_language
join languages lang
  on lang.name = pg_temp.blank_to_null(old_language.description)
where pg_temp.blank_to_null(old_language.old_language_code) is not null
  and pg_temp.blank_to_null(old_language.description) is not null
on conflict (old_language_code) do update set
  language_id = excluded.language_id;

create temp table _old_pat as
select *
from dblink(
  pg_temp.legacy_conn(),
  'select "acct", "dep_no", "first", "mi", "last", "suffix", "nickname", "birth_dt",
          "sex", "sex2", "pronouns", "married", "employed", "language", "ethnicity",
          "active", "hidden", "pat_status", "pat_class", "pat_cat", "pat_stage",
          "prv", "office", "pharm", "pharm2", "pharm3", "address1", "address2", "city", "state", "zip",
          "phone1", "phone2", "cell_phone", "email"
   from "pat"'
) as t(
  acct text,
  dep_no text,
  first_name text,
  middle_name text,
  last_name text,
  suffix text,
  nickname text,
  date_of_birth date,
  sex text,
  gender_identity text,
  pronouns text,
  marital_status text,
  employment_status text,
  preferred_language text,
  ethnicity text,
  active boolean,
  hidden boolean,
  patient_status text,
  classification text,
  category text,
  stage text,
  provider_code text,
  office text,
  primary_pharmacy_code text,
  secondary_pharmacy_code text,
  mail_order_pharmacy_code text,
  address_line1 text,
  address_line2 text,
  city text,
  state text,
  postal_code text,
  home_phone text,
  work_phone text,
  mobile_phone text,
  email text
);

create temp table _old_ins as
select *
from dblink(
  pg_temp.legacy_conn(),
  'select "code", "name", "desc", "payer_id", "phone", "email",
          "address1", "address2", "city", "state", "zip"
   from "ins"'
) as t(
  code text,
  name text,
  description text,
  payer_id text,
  phone text,
  email text,
  address_line1 text,
  address_line2 text,
  city text,
  state text,
  postal_code text
);

create temp table _old_patins as
select *
from dblink(
  pg_temp.legacy_conn(),
  'select "acct", "dep_no", "plan_no", "ins", "order", "id", "group_id", "group_name",
          "first", "mi", "last", "birth_dt", "sex", "rel", "effect_dt", "expire_dt",
          "copay"
   from "patins"'
) as t(
  acct text,
  dep_no text,
  plan_no text,
  carrier_code text,
  priority text,
  member_id text,
  group_number text,
  group_name text,
  subscriber_first_name text,
  subscriber_middle_name text,
  subscriber_last_name text,
  subscriber_date_of_birth date,
  subscriber_sex text,
  relationship_to_patient text,
  effective_date date,
  expiration_date date,
  copay numeric
);

create temp table _old_apttype as
select *
from dblink(
  pg_temp.legacy_conn(),
  'select "code", "desc", "length", "visit_type", false as "inactive"
   from "apttype"'
) as t(
  code text,
  name text,
  default_duration_minutes integer,
  visit_type text,
  inactive boolean
);

create temp table _old_apt as
select *
from dblink(
  pg_temp.legacy_conn(),
  'select "acct", "dep_no", "date", "time1", "time2", "length", "office", "prv",
          "room", "apttype", "visit_type", "arrived", "triaged", "checkedout",
          "complete", "apt_status", "desc", "note", "visit_no", "conf_dt",
          "signed_dt"
   from "apt"'
) as t(
  acct text,
  dep_no text,
  appointment_date date,
  time1 numeric,
  time2 numeric,
  length_minutes integer,
  office text,
  provider_code text,
  room text,
  appointment_type_code text,
  visit_type text,
  arrived boolean,
  triaged boolean,
  checkedout boolean,
  complete boolean,
  legacy_status text,
  reason text,
  note text,
  visit_no text,
  confirmed_date date,
  signed_date date
);

create temp table _old_visit as
select *
from dblink(
  pg_temp.legacy_conn(),
  'select "acct", "dep_no", "visit_no", "date", "office", "prv", "nurse",
          "visit_type", "closed", "smoking", "sbp", "dbp", "heart_rate",
          "resp_rate", "temp", "pulse_ox", "height", "weight", "bmi", "pain"
   from "visit"'
) as t(
  acct text,
  dep_no text,
  visit_no text,
  visit_date date,
  office text,
  provider_code text,
  nurse_code text,
  visit_type text,
  closed boolean,
  smoking_status text,
  systolic_bp text,
  diastolic_bp text,
  heart_rate text,
  respiratory_rate text,
  temperature text,
  pulse_ox text,
  height text,
  weight text,
  bmi text,
  pain_score text
);

create temp table _old_patdx as
select *
from dblink(
  pg_temp.legacy_conn(),
  'select "acct", "dep_no", "mr_no", "dx", "diag", "desc", "date1", "date2", "note"
   from "patdx"'
) as t(
  acct text,
  dep_no text,
  mr_no text,
  icd10 text,
  legacy_diag text,
  description text,
  onset_date date,
  resolved_date date,
  note text
);

create temp table _old_allergy as
select *
from dblink(
  pg_temp.legacy_conn(),
  'select "acct", "dep_no", "mr_no", "type", "ax", "rx_desc", "desc",
          "severity", "inactive", "note"
   from "allergy"'
) as t(
  acct text,
  dep_no text,
  mr_no text,
  allergen_type text,
  allergen_code text,
  rx_description text,
  description text,
  severity text,
  inactive boolean,
  note text
);

create temp table _old_patrx as
select *
from dblink(
  pg_temp.legacy_conn(),
  'select "acct", "dep_no", "visit_no", "mr_no", "desc", "strength", "dosage",
          "route", "frequency", "date", "end_dt", "refill_max", "controlled",
          "discontd", "voided", "instruct", "note"
   from "patrx"'
) as t(
  acct text,
  dep_no text,
  visit_no text,
  mr_no text,
  medication_name text,
  strength text,
  dose text,
  route text,
  frequency text,
  start_date date,
  end_date date,
  refills integer,
  controlled boolean,
  discontinued text,
  voided boolean,
  instructions text,
  note text
);

create temp table _old_refout as
select *
from dblink(
  pg_temp.legacy_conn(),
  'select "acct", "dep_no", "visit_no", "mr_no", "rec_type", "code", "desc",
          "dx", "diag", "prv", "urgent", "complete", "active", "ord_dt",
          "sent_dt", "resulted", "note", "comments", "test_data"
   from "refout"'
) as t(
  acct text,
  dep_no text,
  visit_no text,
  mr_no text,
  rec_type text,
  code text,
  description text,
  icd10 text,
  legacy_diag text,
  provider_code text,
  urgent boolean,
  complete boolean,
  active boolean,
  ordered_date date,
  sent_date date,
  resulted boolean,
  note text,
  comments text,
  test_data text
);

create temp table _old_claim as
select *
from dblink(
  pg_temp.legacy_conn(),
  'select "acct", "dep_no", "claim_no", "clm_status", "date", "ins1", "ins2", "ins3",
          "plan_no1", "plan_no2", "plan_no3", "ins_bal", "pat_bal", "charge", "pay",
          "adjust", "pat_pay", "copay", "deduct", "office", "prv", "rnd_prv",
          "ref", "servfac", "auth_no", "pos", "diag1", "diag2", "diag3", "diag4",
          "desc", "clm_type", "source", "visit_no", "case_no", "bill_stage",
          "stage_dt", "stage_exp", "icd_type", "mr_no", "note", "incomplete",
          "courtesy", "updated", "ins1_bill", "ins2_bill", "ins3_bill",
          "ins1_recd", "ins2_recd", "ins3_recd", "ins1_pay", "ins2_pay",
          "ins3_pay"
   from "claim"'
) as t(
  acct text,
  dep_no text,
  claim_no text,
  legacy_status text,
  service_date date,
  primary_carrier_code text,
  secondary_carrier_code text,
  tertiary_carrier_code text,
  primary_plan_no text,
  secondary_plan_no text,
  tertiary_plan_no text,
  insurance_balance numeric,
  patient_balance numeric,
  total_charge numeric,
  insurance_paid numeric,
  total_adjustment numeric,
  patient_paid numeric,
  copay numeric,
  deductible numeric,
  office text,
  provider_code text,
  rendering_provider_code text,
  referring_provider_code text,
  service_facility_code text,
  authorization_number text,
  place_of_service text,
  diagnosis1 text,
  diagnosis2 text,
  diagnosis3 text,
  diagnosis4 text,
  description text,
  claim_type text,
  source text,
  visit_no text,
  case_no text,
  bill_stage text,
  stage_date date,
  stage_expiration_date date,
  icd_type text,
  mr_no text,
  note text,
  incomplete boolean,
  courtesy boolean,
  updated boolean,
  primary_billed_date date,
  secondary_billed_date date,
  tertiary_billed_date date,
  primary_received_date date,
  secondary_received_date date,
  tertiary_received_date date,
  primary_paid numeric,
  secondary_paid numeric,
  tertiary_paid numeric
);

create temp table _old_clmdx as
select *
from dblink(
  pg_temp.legacy_conn(),
  'select "claim_no", "seq_no", "dx"
   from "clmdx"'
) as t(
  claim_no text,
  sequence text,
  diagnosis_code text
);

create temp table _old_trans as
select *
from dblink(
  pg_temp.legacy_conn(),
  'select "acct", "dep_no", "claim_no", "line_no", "proc", "trans_type", "desc",
          "prv", "amount", "ins_amt", "pat_amt", "pat_pay", "pat_bal", "resp",
          "date", "date2", "diag_ref", "units", "mod1", "mod2", "mod3", "mod4",
          "tos", "proc_cat", "office", "allowed", "copay", "deduct", "disalwd",
          "eob_no", "ins", "trans_sign", "clm_status", "no_bill"
   from "trans"'
) as t(
  acct text,
  dep_no text,
  claim_no text,
  line_no text,
  procedure_code text,
  transaction_type text,
  description text,
  provider_code text,
  amount numeric,
  insurance_amount numeric,
  patient_amount numeric,
  patient_paid numeric,
  patient_balance numeric,
  responsibility text,
  service_date date,
  secondary_date date,
  diagnosis_pointer text,
  units numeric,
  modifier1 text,
  modifier2 text,
  modifier3 text,
  modifier4 text,
  type_of_service text,
  procedure_category text,
  office text,
  allowed_amount numeric,
  copay numeric,
  deductible numeric,
  disallowed_amount numeric,
  eob_no text,
  carrier_code text,
  transaction_sign text,
  claim_status text,
  no_bill boolean
);

create temp table _old_eob as
select *
from dblink(
  pg_temp.legacy_conn(),
  'select "eob_no", "ins_comp", "check_no", "check_amt", "check_dt", "acct",
          "dep_no", "claim_no", "date", "ins_pay", "pat_resp", "complete",
          "verified"
   from "eob"'
) as t(
  eob_no text,
  carrier_code text,
  check_no text,
  check_amount numeric,
  check_date date,
  acct text,
  dep_no text,
  claim_no text,
  posted_date date,
  insurance_paid numeric,
  patient_responsibility numeric,
  complete boolean,
  verified boolean
);

insert into users (username, password)
select
  coalesce(pg_temp.blank_to_null(username), 'unknown-user'),
  crypt(lower(username), gen_salt('bf'))
from _old_users;

do $$
declare
  row_data record;
  new_id bigint;
begin
  for row_data in
    select *
    from _old_prv
    where pg_temp.blank_to_null(code) is not null
  loop
    insert into providers (name, role, active)
    values (
      coalesce(
        pg_temp.blank_to_null(row_data.description),
        concat_ws(' ', pg_temp.blank_to_null(row_data.first_name), pg_temp.blank_to_null(row_data.last_name)),
        row_data.code
      ),
      pg_temp.blank_to_null(row_data.provider_type),
      not coalesce(row_data.inactive, false)
    )
    returning id into new_id;

    insert into _provider_map (legacy_code, provider_id)
    values (row_data.code, new_id);
  end loop;
end $$;

do $$
declare
  row_data record;
  new_id bigint;
begin
  for row_data in
    select distinct pg_temp.blank_to_null(office) as office
    from (
      select office from _old_pat
      union all select office from _old_prv
      union all select office from _old_apt
      union all select office from _old_visit
    ) offices
    where pg_temp.blank_to_null(office) is not null
  loop
    insert into locations (name, active)
    values (row_data.office, true)
    returning id into new_id;

    insert into _location_map (legacy_code, location_id)
    values (row_data.office, new_id);
  end loop;
end $$;

do $$
declare
  row_data record;
  new_id bigint;
  location_id_value bigint;
begin
  for row_data in
    select distinct
      coalesce(pg_temp.blank_to_null(office), 'UNKNOWN') as office,
      pg_temp.blank_to_null(room) as room
    from _old_apt
    where pg_temp.blank_to_null(room) is not null
  loop
    select location_id into location_id_value
    from _location_map
    where legacy_code = row_data.office;

    insert into rooms (location_id, name, active)
    values (location_id_value, row_data.room, true)
    returning id into new_id;

    insert into _room_map (legacy_office, legacy_room, room_id)
    values (row_data.office, row_data.room, new_id);
  end loop;
end $$;

do $$
declare
  row_data record;
  new_id bigint;
begin
  for row_data in
    select *
    from _old_apttype
    where pg_temp.blank_to_null(code) is not null
  loop
    insert into appointment_types (
      name,
      default_duration_minutes,
      visit_type,
      active
    )
    values (
      coalesce(pg_temp.blank_to_null(row_data.name), row_data.code),
      coalesce(nullif(row_data.default_duration_minutes, 0), 15),
      pg_temp.blank_to_null(row_data.visit_type),
      not coalesce(row_data.inactive, false)
    )
    returning id into new_id;

    insert into _appointment_type_map (legacy_code, appointment_type_id)
    values (row_data.code, new_id);
  end loop;
end $$;

do $$
declare
  row_data record;
  new_id bigint;
begin
  for row_data in
    select *
    from _old_pharm
    where pg_temp.blank_to_null(code) is not null
      and pg_temp.blank_to_null(name) is not null
  loop
    insert into pharmacies (
      name,
      address_line1,
      address_line2,
      city,
      state,
      postal_code,
      phone,
      fax_number,
      area,
      external_identifier,
      active
    )
    values (
      pg_temp.blank_to_null(row_data.name),
      pg_temp.blank_to_null(row_data.address_line1),
      pg_temp.blank_to_null(row_data.address_line2),
      pg_temp.blank_to_null(row_data.city),
      pg_temp.blank_to_null(row_data.state),
      pg_temp.blank_to_null(row_data.postal_code),
      pg_temp.blank_to_null(row_data.phone),
      pg_temp.blank_to_null(row_data.fax_number),
      pg_temp.blank_to_null(row_data.area),
      pg_temp.blank_to_null(row_data.external_identifier),
      not coalesce(row_data.inactive, false)
    )
    on conflict (external_identifier) do update set
      name = excluded.name,
      address_line1 = excluded.address_line1,
      address_line2 = excluded.address_line2,
      city = excluded.city,
      state = excluded.state,
      postal_code = excluded.postal_code,
      phone = excluded.phone,
      fax_number = excluded.fax_number,
      area = excluded.area,
      active = excluded.active,
      updated_at = now()
    returning id into new_id;

    insert into _pharmacy_map (legacy_code, pharmacy_id)
    values (row_data.code, new_id);
  end loop;
end $$;

do $$
declare
  row_data record;
  new_id bigint;
begin
  for row_data in
    select *
    from _old_ins
    where pg_temp.blank_to_null(code) is not null
  loop
    insert into insurance_carriers (
      name,
      payer_id,
      phone,
      email,
      address_line1,
      address_line2,
      city,
      state,
      postal_code
    )
    values (
      coalesce(pg_temp.blank_to_null(row_data.name), pg_temp.blank_to_null(row_data.description), row_data.code),
      pg_temp.blank_to_null(row_data.payer_id),
      pg_temp.blank_to_null(row_data.phone),
      pg_temp.blank_to_null(row_data.email),
      pg_temp.blank_to_null(row_data.address_line1),
      pg_temp.blank_to_null(row_data.address_line2),
      pg_temp.blank_to_null(row_data.city),
      pg_temp.blank_to_null(row_data.state),
      pg_temp.blank_to_null(row_data.postal_code)
    )
    returning id into new_id;

    insert into _carrier_map (legacy_code, carrier_id)
    values (row_data.code, new_id);
  end loop;
end $$;

do $$
declare
  row_data record;
  new_patient_id bigint;
  provider_id_value bigint;
  location_id_value bigint;
  preferred_language_id_value bigint;
begin
  for row_data in
    select *
    from _old_pat
    where pg_temp.blank_to_null(acct) is not null
      and pg_temp.blank_to_null(first_name) is not null
      and pg_temp.blank_to_null(last_name) is not null
      and date_of_birth is not null
  loop
    select provider_id into provider_id_value
    from _provider_map
    where legacy_code = row_data.provider_code;

    select location_id into location_id_value
    from _location_map
    where legacy_code = row_data.office;

    select language_id into preferred_language_id_value
    from _language_map
    where old_language_code = pg_temp.blank_to_null(row_data.preferred_language)
    limit 1;

    if preferred_language_id_value is null then
      select id into preferred_language_id_value
      from languages
      where lower(name) = 'english'
      limit 1;
    end if;

    insert into patients (
      first_name,
      middle_name,
      last_name,
      suffix,
      nickname,
      date_of_birth,
      sex_at_birth,
      gender_identity,
      pronouns,
      marital_status,
      employment_status,
      preferred_language_id,
      ethnicity,
      status,
      billing_status,
      classification,
      category,
      stage,
      primary_provider_id,
      primary_location_id
    )
    values (
      pg_temp.blank_to_null(row_data.first_name),
      pg_temp.blank_to_null(row_data.middle_name),
      pg_temp.blank_to_null(row_data.last_name),
      pg_temp.blank_to_null(row_data.suffix),
      pg_temp.blank_to_null(row_data.nickname),
      row_data.date_of_birth,
      pg_temp.legacy_sex(row_data.sex),
      pg_temp.blank_to_null(row_data.gender_identity),
      pg_temp.blank_to_null(row_data.pronouns),
      pg_temp.blank_to_null(row_data.marital_status),
      pg_temp.blank_to_null(row_data.employment_status),
      preferred_language_id_value,
      pg_temp.blank_to_null(row_data.ethnicity),
      case
        when coalesce(row_data.active, true) and not coalesce(row_data.hidden, false) then 'active'
        else 'inactive'
      end,
      pg_temp.blank_to_null(row_data.patient_status),
      pg_temp.blank_to_null(row_data.classification),
      pg_temp.blank_to_null(row_data.category),
      pg_temp.blank_to_null(row_data.stage),
      provider_id_value,
      location_id_value
    )
    returning id into new_patient_id;

    insert into _patient_map (legacy_acct, legacy_dep_no, patient_id)
    values (row_data.acct, coalesce(pg_temp.blank_to_null(row_data.dep_no), '00'), new_patient_id);

    insert into patient_contacts (
      patient_id,
      address_line1,
      address_line2,
      city,
      state,
      postal_code,
      home_phone,
      work_phone,
      mobile_phone,
      email
    )
    values (
      new_patient_id,
      pg_temp.blank_to_null(row_data.address_line1),
      pg_temp.blank_to_null(row_data.address_line2),
      pg_temp.blank_to_null(row_data.city),
      pg_temp.blank_to_null(row_data.state),
      pg_temp.blank_to_null(row_data.postal_code),
      pg_temp.blank_to_null(row_data.home_phone),
      pg_temp.blank_to_null(row_data.work_phone),
      pg_temp.blank_to_null(row_data.mobile_phone),
      pg_temp.blank_to_null(row_data.email)
    );

    insert into patient_pharmacies (
      patient_id,
      pharmacy_id,
      type,
      priority
    )
    select
      new_patient_id,
      pharmacy_map.pharmacy_id,
      pharmacy_slot.pharmacy_type,
      pharmacy_slot.priority
    from (
      values
        (pg_temp.blank_to_null(row_data.primary_pharmacy_code), 'primary', 1),
        (pg_temp.blank_to_null(row_data.secondary_pharmacy_code), 'secondary', 2),
        (pg_temp.blank_to_null(row_data.mail_order_pharmacy_code), 'mail_order', 3)
    ) as pharmacy_slot(legacy_code, pharmacy_type, priority)
    join _pharmacy_map pharmacy_map
      on pharmacy_map.legacy_code = pharmacy_slot.legacy_code
    where pharmacy_slot.legacy_code is not null
    on conflict (patient_id, priority) do nothing;
  end loop;
end $$;

do $$
declare
  row_data record;
  patient_id_value bigint;
begin
  for row_data in
    select *
    from _old_patins
    where pg_temp.blank_to_null(carrier_code) is not null
  loop
    select patient_id into patient_id_value
    from _patient_map
    where legacy_acct = row_data.acct
      and legacy_dep_no = coalesce(pg_temp.blank_to_null(row_data.dep_no), '00');

    if patient_id_value is null then
      select patient_id into patient_id_value
      from _patient_map
      where legacy_acct = row_data.acct
      order by legacy_dep_no
      limit 1;
    end if;

    if patient_id_value is not null then
      insert into patient_insurance_policies (
        patient_id,
        carrier_id,
        priority,
        member_id,
        group_number,
        group_name,
        subscriber_first_name,
        subscriber_middle_name,
        subscriber_last_name,
        subscriber_date_of_birth,
        subscriber_sex_at_birth,
        relationship_to_patient,
        effective_date,
        expiration_date,
        copay,
        is_active
      )
      values (
        patient_id_value,
        (select carrier_id from _carrier_map where legacy_code = row_data.carrier_code),
        coalesce(nullif(regexp_replace(coalesce(row_data.priority, ''), '[^0-9]', '', 'g'), '')::smallint, 1),
        pg_temp.blank_to_null(row_data.member_id),
        pg_temp.blank_to_null(row_data.group_number),
        pg_temp.blank_to_null(row_data.group_name),
        pg_temp.blank_to_null(row_data.subscriber_first_name),
        pg_temp.blank_to_null(row_data.subscriber_middle_name),
        pg_temp.blank_to_null(row_data.subscriber_last_name),
        row_data.subscriber_date_of_birth,
        pg_temp.legacy_sex(row_data.subscriber_sex),
        pg_temp.blank_to_null(row_data.relationship_to_patient),
        row_data.effective_date,
        row_data.expiration_date,
        row_data.copay,
        row_data.expiration_date is null or row_data.expiration_date >= current_date
      )
      on conflict (patient_id, priority) do nothing;
    end if;
  end loop;
end $$;

insert into _policy_map (
  legacy_acct,
  legacy_dep_no,
  legacy_carrier_code,
  legacy_plan_no,
  priority,
  policy_id
)
select
  old_policy.acct,
  coalesce(pg_temp.blank_to_null(old_policy.dep_no), '00'),
  pg_temp.blank_to_null(old_policy.carrier_code),
  pg_temp.blank_to_null(old_policy.plan_no),
  coalesce(nullif(regexp_replace(coalesce(old_policy.priority, ''), '[^0-9]', '', 'g'), '')::smallint, 1),
  policy.id
from _old_patins old_policy
join _patient_map patient_map
  on patient_map.legacy_acct = old_policy.acct
 and patient_map.legacy_dep_no = coalesce(pg_temp.blank_to_null(old_policy.dep_no), '00')
join patient_insurance_policies policy
  on policy.patient_id = patient_map.patient_id
 and policy.priority = coalesce(nullif(regexp_replace(coalesce(old_policy.priority, ''), '[^0-9]', '', 'g'), '')::smallint, 1)
where pg_temp.blank_to_null(old_policy.carrier_code) is not null;

do $$
declare
  row_data record;
  new_appointment_id bigint;
  start_value timestamptz;
  patient_id_value bigint;
  provider_id_value bigint;
  location_id_value bigint;
  room_id_value bigint;
begin
  for row_data in
    select *
    from _old_apt
    where appointment_date is not null
  loop
    start_value := pg_temp.legacy_timestamptz(row_data.appointment_date, row_data.time1);

    select patient_id into patient_id_value
    from _patient_map
    where legacy_acct = row_data.acct
      and legacy_dep_no = coalesce(pg_temp.blank_to_null(row_data.dep_no), '00');

    select provider_id into provider_id_value
    from _provider_map
    where legacy_code = row_data.provider_code;

    select location_id into location_id_value
    from _location_map
    where legacy_code = row_data.office;

    select room_id into room_id_value
    from _room_map
    where legacy_office = coalesce(pg_temp.blank_to_null(row_data.office), 'UNKNOWN')
      and legacy_room = pg_temp.blank_to_null(row_data.room);

    insert into appointments (
      patient_id,
      appointment_type_id,
      provider_id,
      location_id,
      room_id,
      scheduled_start,
      scheduled_end,
      status,
      reason,
      notes,
      confirmed_at,
      signed_at
    )
    values (
      patient_id_value,
      (
        select appointment_type_id
        from _appointment_type_map
        where legacy_code = row_data.appointment_type_code
      ),
      provider_id_value,
      location_id_value,
      room_id_value,
      start_value,
      start_value + make_interval(mins => coalesce(nullif(row_data.length_minutes, 0), 15)),
      pg_temp.appointment_status(
        row_data.legacy_status,
        row_data.arrived,
        row_data.triaged,
        row_data.checkedout,
        row_data.complete
      ),
      pg_temp.blank_to_null(row_data.reason),
      pg_temp.blank_to_null(row_data.note),
      case when row_data.confirmed_date is not null then row_data.confirmed_date::timestamptz else null end,
      case when row_data.signed_date is not null then row_data.signed_date::timestamptz else null end
    )
    returning id into new_appointment_id;

    if pg_temp.blank_to_null(row_data.visit_no) is not null then
      insert into _appointment_map (legacy_visit_no, appointment_id)
      values (row_data.visit_no, new_appointment_id)
      on conflict (legacy_visit_no) do nothing;
    end if;
  end loop;
end $$;

do $$
declare
  row_data record;
  new_visit_id bigint;
  patient_id_value bigint;
begin
  for row_data in
    select *
    from _old_visit
    where pg_temp.blank_to_null(visit_no) is not null
      and visit_date is not null
  loop
    select patient_id into patient_id_value
    from _patient_map
    where legacy_acct = row_data.acct
      and legacy_dep_no = coalesce(pg_temp.blank_to_null(row_data.dep_no), '00');

    if patient_id_value is null then
      continue;
    end if;

    insert into visits (
      patient_id,
      appointment_id,
      provider_id,
      nurse_id,
      location_id,
      visit_date,
      visit_type,
      status,
      smoking_status
    )
    values (
      patient_id_value,
      (select appointment_id from _appointment_map where legacy_visit_no = row_data.visit_no),
      (select provider_id from _provider_map where legacy_code = row_data.provider_code),
      (select provider_id from _provider_map where legacy_code = row_data.nurse_code),
      (select location_id from _location_map where legacy_code = row_data.office),
      row_data.visit_date,
      pg_temp.blank_to_null(row_data.visit_type),
      case when coalesce(row_data.closed, false) then 'closed' else 'open' end,
      pg_temp.blank_to_null(row_data.smoking_status)
    )
    returning id into new_visit_id;

    insert into _visit_map (legacy_visit_no, visit_id)
    values (row_data.visit_no, new_visit_id)
    on conflict (legacy_visit_no) do nothing;

    insert into vital_signs (
      visit_id,
      systolic_bp,
      diastolic_bp,
      heart_rate,
      respiratory_rate,
      temperature_c,
      pulse_ox,
      height_cm,
      weight_kg,
      bmi,
      pain_score
    )
    values (
      new_visit_id,
      pg_temp.legacy_systolic_bp(row_data.systolic_bp),
      pg_temp.legacy_diastolic_bp(row_data.systolic_bp, row_data.diastolic_bp),
      pg_temp.bounded_numeric_or_null(row_data.heart_rate, 20, 250),
      pg_temp.bounded_numeric_or_null(row_data.respiratory_rate, 5, 80),
      pg_temp.legacy_temperature_c(row_data.temperature),
      pg_temp.bounded_numeric_or_null(row_data.pulse_ox, 50, 100),
      pg_temp.legacy_height_cm(row_data.height),
      pg_temp.legacy_weight_kg(row_data.weight),
      pg_temp.bounded_numeric_or_null(row_data.bmi, 5, 100),
      pg_temp.legacy_pain_score(row_data.pain_score)
    );
  end loop;
end $$;

with transaction_totals as (
  select
    pg_temp.blank_to_null(claim_no) as claim_no,
    sum(coalesce(allowed_amount, 0)) filter (where upper(coalesce(transaction_type, '')) = 'CH') as total_allowed,
    sum(abs(coalesce(amount, 0))) filter (where upper(coalesce(transaction_type, '')) in ('PI', 'PP')) as transaction_paid,
    sum(abs(coalesce(amount, 0))) filter (where upper(coalesce(transaction_type, '')) in ('AW', 'AX', 'AR', 'AM')) as transaction_adjustment
  from _old_trans
  where pg_temp.blank_to_null(claim_no) is not null
  group by pg_temp.blank_to_null(claim_no)
),
source_claims as (
  select distinct on (pg_temp.blank_to_null(old_claim.claim_no))
    pg_temp.blank_to_null(old_claim.claim_no) as claim_number,
    patient_map.patient_id,
    visit_map.visit_id,
    appointment_map.appointment_id,
    policy_map.policy_id,
    provider_map.provider_id,
    location_map.location_id,
    coalesce(old_claim.service_date, current_date) as service_date,
    coalesce(old_claim.total_charge, 0) as total_charge,
    coalesce(transaction_totals.total_allowed, 0) as total_allowed,
    coalesce(
      nullif(coalesce(old_claim.insurance_paid, 0) + coalesce(old_claim.patient_paid, 0), 0),
      transaction_totals.transaction_paid,
      0
    ) as total_paid,
    coalesce(nullif(old_claim.total_adjustment, 0), transaction_totals.transaction_adjustment, 0) as total_adjustment,
    coalesce(old_claim.insurance_balance, 0) as insurance_balance,
    coalesce(old_claim.patient_balance, 0) as patient_balance,
    coalesce(
      old_claim.primary_billed_date,
      old_claim.secondary_billed_date,
      old_claim.tertiary_billed_date,
      old_claim.primary_received_date,
      old_claim.secondary_received_date,
      old_claim.tertiary_received_date
    ) is not null as submitted,
    old_claim.legacy_status,
    old_claim.bill_stage,
    old_claim.incomplete,
    concat_ws(
      E'\n',
      pg_temp.blank_to_null(old_claim.description),
      pg_temp.blank_to_null(old_claim.note),
      case when pg_temp.blank_to_null(old_claim.authorization_number) is not null then 'Auth: ' || pg_temp.blank_to_null(old_claim.authorization_number) end
    ) as note
  from _old_claim old_claim
  join _patient_map patient_map
    on patient_map.legacy_acct = old_claim.acct
   and patient_map.legacy_dep_no = coalesce(pg_temp.blank_to_null(old_claim.dep_no), '00')
  left join _visit_map visit_map
    on visit_map.legacy_visit_no = old_claim.visit_no
  left join _appointment_map appointment_map
    on appointment_map.legacy_visit_no = old_claim.visit_no
  left join _provider_map provider_map
    on provider_map.legacy_code = coalesce(
      pg_temp.blank_to_null(old_claim.rendering_provider_code),
      pg_temp.blank_to_null(old_claim.provider_code)
    )
  left join _location_map location_map
    on location_map.legacy_code = old_claim.office
  left join transaction_totals
    on transaction_totals.claim_no = pg_temp.blank_to_null(old_claim.claim_no)
  left join lateral (
    select mapped_policy.policy_id
    from _policy_map mapped_policy
    where mapped_policy.legacy_acct = old_claim.acct
      and mapped_policy.legacy_dep_no = coalesce(pg_temp.blank_to_null(old_claim.dep_no), '00')
      and (
        mapped_policy.legacy_carrier_code = pg_temp.blank_to_null(old_claim.primary_carrier_code)
        or mapped_policy.legacy_plan_no = pg_temp.blank_to_null(old_claim.primary_plan_no)
        or mapped_policy.priority = 1
      )
    order by
      case
        when mapped_policy.legacy_carrier_code = pg_temp.blank_to_null(old_claim.primary_carrier_code)
          and mapped_policy.legacy_plan_no = pg_temp.blank_to_null(old_claim.primary_plan_no) then 0
        when mapped_policy.legacy_carrier_code = pg_temp.blank_to_null(old_claim.primary_carrier_code) then 1
        when mapped_policy.legacy_plan_no = pg_temp.blank_to_null(old_claim.primary_plan_no) then 2
        else 3
      end,
      mapped_policy.priority
    limit 1
  ) policy_map on true
  where pg_temp.blank_to_null(old_claim.claim_no) is not null
  order by pg_temp.blank_to_null(old_claim.claim_no), old_claim.service_date nulls last
),
normalized_claims as (
  select
    source_claims.*,
    pg_temp.billing_claim_status(
      source_claims.legacy_status,
      source_claims.total_charge,
      source_claims.total_paid,
      source_claims.total_adjustment,
      source_claims.insurance_balance,
      source_claims.patient_balance,
      source_claims.submitted
    ) as status
  from source_claims
),
inserted_claims as (
  insert into billing_claims (
    claim_number,
    patient_id,
    visit_id,
    appointment_id,
    insurance_policy_id,
    provider_id,
    location_id,
    service_date,
    status,
    billing_stage,
    total_charge,
    total_allowed,
    total_paid,
    total_adjustment,
    insurance_balance,
    patient_balance,
    note
  )
  select
    claim_number,
    patient_id,
    visit_id,
    appointment_id,
    policy_id,
    provider_id,
    location_id,
    service_date,
    status,
    pg_temp.billing_stage(bill_stage, incomplete, status, submitted),
    total_charge,
    total_allowed,
    total_paid,
    total_adjustment,
    insurance_balance,
    patient_balance,
    nullif(note, '')
  from normalized_claims
  returning claim_number, id
)
insert into _claim_map (legacy_claim_no, claim_id)
select claim_number, id
from inserted_claims;

with legacy_diagnoses as (
  select distinct
    claim_map.claim_id,
    pg_temp.blank_to_null(old_diagnosis.diagnosis_code) as diagnosis_code,
    coalesce(
      nullif(regexp_replace(coalesce(old_diagnosis.sequence, ''), '[^0-9]', '', 'g'), '')::smallint,
      99
    ) as legacy_sequence
  from _old_clmdx old_diagnosis
  join _claim_map claim_map
    on claim_map.legacy_claim_no = pg_temp.blank_to_null(old_diagnosis.claim_no)
  where pg_temp.blank_to_null(old_diagnosis.diagnosis_code) is not null
),
numbered_diagnoses as (
  select
    claim_id,
    diagnosis_code,
    (row_number() over (
      partition by claim_id
      order by legacy_sequence, diagnosis_code
    ))::smallint as sequence
  from legacy_diagnoses
)
insert into billing_claim_diagnoses (
  claim_id,
  sequence,
  diagnosis_code
)
select
  claim_id,
  sequence,
  diagnosis_code
from numbered_diagnoses
where sequence <= 12
on conflict (claim_id, sequence) do nothing;

insert into billing_claim_diagnoses (
  claim_id,
  sequence,
  diagnosis_code
)
select
  claim_map.claim_id,
  diagnosis_slot.sequence,
  diagnosis_slot.diagnosis_code
from _old_claim old_claim
join _claim_map claim_map
  on claim_map.legacy_claim_no = pg_temp.blank_to_null(old_claim.claim_no)
cross join lateral (
  values
    (1::smallint, pg_temp.blank_to_null(old_claim.diagnosis1)),
    (2::smallint, pg_temp.blank_to_null(old_claim.diagnosis2)),
    (3::smallint, pg_temp.blank_to_null(old_claim.diagnosis3)),
    (4::smallint, pg_temp.blank_to_null(old_claim.diagnosis4))
) as diagnosis_slot(sequence, diagnosis_code)
where diagnosis_slot.diagnosis_code is not null
on conflict (claim_id, sequence) do nothing;

with line_source as (
  select
    claim_map.claim_id,
    pg_temp.blank_to_null(trans.claim_no) as claim_number,
    coalesce(pg_temp.blank_to_null(trans.line_no), '00') as line_no,
    min(trans.service_date) filter (where upper(coalesce(trans.transaction_type, '')) = 'CH') as service_date,
    coalesce(
      max(pg_temp.blank_to_null(trans.procedure_code)) filter (where upper(coalesce(trans.transaction_type, '')) = 'CH'),
      max(pg_temp.blank_to_null(trans.procedure_code)),
      'LEGACY'
    ) as procedure_code,
    coalesce(
      max(pg_temp.blank_to_null(trans.description)) filter (where upper(coalesce(trans.transaction_type, '')) = 'CH'),
      max(pg_temp.blank_to_null(trans.description)),
      'Imported charge'
    ) as description,
    coalesce(
      nullif(max(trans.units) filter (where upper(coalesce(trans.transaction_type, '')) = 'CH'), 0),
      1
    ) as units,
    greatest(
      coalesce(sum(coalesce(trans.amount, 0)) filter (where upper(coalesce(trans.transaction_type, '')) = 'CH'), 0),
      0
    ) as charge_amount,
    greatest(
      coalesce(max(trans.allowed_amount) filter (where upper(coalesce(trans.transaction_type, '')) = 'CH'), 0),
      0
    ) as allowed_amount,
    coalesce(sum(abs(coalesce(trans.amount, 0))) filter (where upper(coalesce(trans.transaction_type, '')) in ('PI', 'PP')), 0) as paid_amount,
    coalesce(sum(abs(coalesce(trans.amount, 0))) filter (where upper(coalesce(trans.transaction_type, '')) in ('AW', 'AX', 'AR', 'AM')), 0)
      + coalesce(sum(coalesce(trans.disallowed_amount, 0)) filter (where upper(coalesce(trans.transaction_type, '')) = 'CH'), 0) as adjustment_amount,
    coalesce(sum(coalesce(trans.copay, 0) + coalesce(trans.deductible, 0)) filter (where upper(coalesce(trans.transaction_type, '')) = 'CH'), 0)
      + coalesce(sum(coalesce(trans.amount, 0)) filter (where upper(coalesce(trans.transaction_type, '')) in ('AC', 'AD')), 0) as patient_responsibility_amount,
    greatest(coalesce(max(trans.patient_balance), 0), 0) as patient_balance,
    max(pg_temp.blank_to_null(trans.diagnosis_pointer)) as diagnosis_pointer,
    max(pg_temp.blank_to_null(trans.provider_code)) as rendering_provider_code
  from _old_trans trans
  join _claim_map claim_map
    on claim_map.legacy_claim_no = pg_temp.blank_to_null(trans.claim_no)
  where pg_temp.blank_to_null(trans.claim_no) is not null
  group by
    claim_map.claim_id,
    pg_temp.blank_to_null(trans.claim_no),
    coalesce(pg_temp.blank_to_null(trans.line_no), '00')
  having count(*) filter (where upper(coalesce(trans.transaction_type, '')) = 'CH') > 0
)
insert into billing_claim_lines (
  claim_id,
  service_date,
  procedure_code,
  description,
  units,
  charge_amount,
  allowed_amount,
  paid_amount,
  adjustment_amount,
  patient_responsibility_amount,
  insurance_balance,
  patient_balance,
  diagnosis_pointer,
  rendering_provider_id
)
select
  line_source.claim_id,
  coalesce(line_source.service_date, claim.service_date),
  line_source.procedure_code,
  line_source.description,
  line_source.units,
  line_source.charge_amount,
  line_source.allowed_amount,
  line_source.paid_amount,
  line_source.adjustment_amount,
  line_source.patient_responsibility_amount,
  greatest(
    line_source.allowed_amount
      - line_source.paid_amount
      - line_source.adjustment_amount
      - line_source.patient_responsibility_amount,
    0
  ),
  line_source.patient_balance,
  line_source.diagnosis_pointer,
  provider_map.provider_id
from line_source
join billing_claims claim
  on claim.id = line_source.claim_id
left join _provider_map provider_map
  on provider_map.legacy_code = line_source.rendering_provider_code;

insert into billing_claim_lines (
  claim_id,
  service_date,
  procedure_code,
  description,
  units,
  charge_amount,
  allowed_amount,
  paid_amount,
  adjustment_amount,
  patient_responsibility_amount,
  insurance_balance,
  patient_balance
)
select
  claim.id,
  claim.service_date,
  'CLAIM',
  'Imported claim balance',
  1,
  claim.total_charge,
  claim.total_allowed,
  claim.total_paid,
  claim.total_adjustment,
  claim.patient_balance,
  claim.insurance_balance,
  claim.patient_balance
from billing_claims claim
where claim.total_charge > 0
  and not exists (
    select 1
    from billing_claim_lines line
    where line.claim_id = claim.id
  );

insert into billing_claim_events (
  claim_id,
  event_type,
  to_status,
  note,
  created_at
)
select
  claim.id,
  'imported',
  claim.status,
  'Claim migrated',
  claim.service_date::timestamptz
from billing_claims claim;

insert into billing_claim_events (
  claim_id,
  event_type,
  note,
  created_at
)
select
  claim_map.claim_id,
  'eob',
  concat_ws(
    ' | ',
    'EOB ' || pg_temp.blank_to_null(old_eob.eob_no),
    case when pg_temp.blank_to_null(old_eob.check_no) is not null then 'Check ' || pg_temp.blank_to_null(old_eob.check_no) end,
    case when old_eob.check_amount is not null then 'Amount ' || old_eob.check_amount::text end,
    case when coalesce(old_eob.verified, false) then 'Verified' end,
    case when coalesce(old_eob.complete, false) then 'Complete' end
  ),
  coalesce(old_eob.check_date, old_eob.posted_date, current_date)::timestamptz
from _old_eob old_eob
join _claim_map claim_map
  on claim_map.legacy_claim_no = pg_temp.blank_to_null(old_eob.claim_no)
where pg_temp.blank_to_null(old_eob.eob_no) is not null;

insert into patient_problems (
  patient_id,
  diagnosis_code,
  description,
  status,
  onset_date,
  resolved_date,
  note
)
select
  patient_map.patient_id,
  coalesce(pg_temp.blank_to_null(old_problem.icd10), pg_temp.blank_to_null(old_problem.legacy_diag)),
  coalesce(pg_temp.blank_to_null(old_problem.description), 'Unspecified problem'),
  case when old_problem.resolved_date is not null then 'resolved' else 'active' end,
  old_problem.onset_date,
  old_problem.resolved_date,
  pg_temp.blank_to_null(old_problem.note)
from _old_patdx old_problem
join _patient_map patient_map
  on patient_map.legacy_acct = old_problem.acct
 and patient_map.legacy_dep_no = coalesce(pg_temp.blank_to_null(old_problem.dep_no), '00');

insert into patient_allergies (
  patient_id,
  allergen,
  allergen_type,
  reaction,
  severity,
  status,
  note
)
select
  patient_map.patient_id,
  coalesce(
    pg_temp.blank_to_null(old_allergy.description),
    pg_temp.blank_to_null(old_allergy.rx_description),
    pg_temp.blank_to_null(old_allergy.allergen_code),
    'Unspecified allergy'
  ),
  pg_temp.blank_to_null(old_allergy.allergen_type),
  pg_temp.blank_to_null(old_allergy.rx_description),
  pg_temp.blank_to_null(old_allergy.severity),
  case when coalesce(old_allergy.inactive, false) then 'inactive' else 'active' end,
  pg_temp.blank_to_null(old_allergy.note)
from _old_allergy old_allergy
join _patient_map patient_map
  on patient_map.legacy_acct = old_allergy.acct
 and patient_map.legacy_dep_no = coalesce(pg_temp.blank_to_null(old_allergy.dep_no), '00');

insert into patient_medications (
  patient_id,
  visit_id,
  medication_name,
  strength,
  dose,
  route,
  frequency,
  start_date,
  end_date,
  refills,
  controlled,
  status,
  instructions,
  note
)
select
  patient_map.patient_id,
  visit_map.visit_id,
  coalesce(pg_temp.blank_to_null(old_rx.medication_name), 'Unspecified medication'),
  pg_temp.blank_to_null(old_rx.strength),
  pg_temp.blank_to_null(old_rx.dose),
  pg_temp.blank_to_null(old_rx.route),
  pg_temp.blank_to_null(old_rx.frequency),
  old_rx.start_date,
  old_rx.end_date,
  old_rx.refills,
  coalesce(old_rx.controlled, false),
  case
    when coalesce(old_rx.voided, false) then 'voided'
    when pg_temp.blank_to_null(old_rx.discontinued) is not null or old_rx.end_date is not null then 'stopped'
    else 'active'
  end,
  pg_temp.blank_to_null(old_rx.instructions),
  pg_temp.blank_to_null(old_rx.note)
from _old_patrx old_rx
join _patient_map patient_map
  on patient_map.legacy_acct = old_rx.acct
 and patient_map.legacy_dep_no = coalesce(pg_temp.blank_to_null(old_rx.dep_no), '00')
left join _visit_map visit_map
  on visit_map.legacy_visit_no = old_rx.visit_no;

insert into clinical_orders (
  patient_id,
  visit_id,
  ordered_by_provider_id,
  order_type,
  code,
  description,
  diagnosis_code,
  priority,
  status,
  ordered_at,
  completed_at,
  note
)
select
  patient_map.patient_id,
  visit_map.visit_id,
  provider_map.provider_id,
  case upper(coalesce(old_order.rec_type, ''))
    when 'L' then 'lab'
    when 'R' then 'referral'
    when 'N' then 'imaging'
    else 'other'
  end,
  pg_temp.blank_to_null(old_order.code),
  coalesce(pg_temp.blank_to_null(old_order.description), 'Unspecified order'),
  coalesce(pg_temp.blank_to_null(old_order.icd10), pg_temp.blank_to_null(old_order.legacy_diag)),
  case when coalesce(old_order.urgent, false) then 'urgent' else 'routine' end,
  case
    when coalesce(old_order.complete, false) then 'completed'
    when coalesce(old_order.resulted, false) then 'resulted'
    when old_order.sent_date is not null then 'sent'
    else 'ordered'
  end,
  coalesce(old_order.ordered_date, current_date)::timestamptz,
  case when coalesce(old_order.complete, false) then current_timestamp else null end,
  coalesce(pg_temp.blank_to_null(old_order.note), pg_temp.blank_to_null(old_order.comments))
from _old_refout old_order
join _patient_map patient_map
  on patient_map.legacy_acct = old_order.acct
 and patient_map.legacy_dep_no = coalesce(pg_temp.blank_to_null(old_order.dep_no), '00')
left join _visit_map visit_map
  on visit_map.legacy_visit_no = old_order.visit_no
left join _provider_map provider_map
  on provider_map.legacy_code = old_order.provider_code
where coalesce(old_order.active, true);

-- Useful quick checks before commit.
select 'users' as table_name, count(*) from users
union all select 'languages', count(*) from languages
union all select 'pharmacies', count(*) from pharmacies
union all select 'providers', count(*) from providers
union all select 'locations', count(*) from locations
union all select 'patients', count(*) from patients
union all select 'patient_pharmacies', count(*) from patient_pharmacies
union all select 'patient_contacts', count(*) from patient_contacts
union all select 'insurance_carriers', count(*) from insurance_carriers
union all select 'patient_insurance_policies', count(*) from patient_insurance_policies
union all select 'appointments', count(*) from appointments
union all select 'visits', count(*) from visits
union all select 'vital_signs', count(*) from vital_signs
union all select 'patient_problems', count(*) from patient_problems
union all select 'patient_allergies', count(*) from patient_allergies
union all select 'patient_medications', count(*) from patient_medications
union all select 'clinical_orders', count(*) from clinical_orders
union all select 'billing_claims', count(*) from billing_claims
union all select 'billing_claim_diagnoses', count(*) from billing_claim_diagnoses
union all select 'billing_claim_lines', count(*) from billing_claim_lines
union all select 'billing_claim_events', count(*) from billing_claim_events;

commit;
