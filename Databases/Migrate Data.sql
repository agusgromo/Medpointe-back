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

create temp table _appointment_map (
  legacy_visit_no text primary key,
  appointment_id bigint not null
) on commit drop;

create temp table _visit_map (
  legacy_visit_no text primary key,
  visit_id bigint not null
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

create temp table _old_pat as
select *
from dblink(
  pg_temp.legacy_conn(),
  'select "acct", "dep_no", "first", "mi", "last", "suffix", "nickname", "birth_dt",
          "sex", "sex2", "pronouns", "married", "employed", "language", "ethnicity",
          "active", "hidden", "pat_status", "pat_class", "pat_cat", "pat_stage",
          "prv", "office", "address1", "address2", "city", "state", "zip",
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
   from "apt"
   where coalesce("visit_no", '''') <> '''''
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

insert into users (username, password)
select
  coalesce(pg_temp.blank_to_null(username), 'unknown-user'),
  crypt(username, gen_salt('bf'))
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
      preferred_language,
      ethnicity,
      status,
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
      coalesce(pg_temp.blank_to_null(row_data.preferred_language), 'English'),
      pg_temp.blank_to_null(row_data.ethnicity),
      case
        when coalesce(row_data.active, true) and not coalesce(row_data.hidden, false) then 'active'
        else 'inactive'
      end,
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

do $$
declare
  row_data record;
  new_appointment_id bigint;
  start_value timestamptz;
  provider_id_value bigint;
  location_id_value bigint;
  room_id_value bigint;
begin
  for row_data in
    select *
    from _old_apt
    where pg_temp.blank_to_null(visit_no) is not null
      and appointment_date is not null
  loop
    start_value := pg_temp.legacy_timestamptz(row_data.appointment_date, row_data.time1);

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
      (
        select patient_id
        from _patient_map
        where legacy_acct = row_data.acct
          and legacy_dep_no = coalesce(pg_temp.blank_to_null(row_data.dep_no), '00')
      ),
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

    insert into _appointment_map (legacy_visit_no, appointment_id)
    values (row_data.visit_no, new_appointment_id)
    on conflict (legacy_visit_no) do nothing;
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
union all select 'providers', count(*) from providers
union all select 'locations', count(*) from locations
union all select 'patients', count(*) from patients
union all select 'patient_contacts', count(*) from patient_contacts
union all select 'insurance_carriers', count(*) from insurance_carriers
union all select 'patient_insurance_policies', count(*) from patient_insurance_policies
union all select 'appointments', count(*) from appointments
union all select 'visits', count(*) from visits
union all select 'vital_signs', count(*) from vital_signs
union all select 'patient_problems', count(*) from patient_problems
union all select 'patient_allergies', count(*) from patient_allergies
union all select 'patient_medications', count(*) from patient_medications
union all select 'clinical_orders', count(*) from clinical_orders;

commit;