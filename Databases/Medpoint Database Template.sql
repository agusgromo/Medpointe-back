/*create database medpointe_old
    WITH 
    OWNER = 'postgres'
    ENCODING = 'UTF8'
    LC_COLLATE = 'en_US.UTF-8'
    LC_CTYPE = 'en_US.UTF-8'
    TEMPLATE = template0;


CREATE DATABASE medpointe
    WITH 
    OWNER = postgres
    ENCODING = 'UTF8'
    LC_COLLATE = 'en_US.UTF-8'
    LC_CTYPE = 'en_US.UTF-8'
    TEMPLATE = template0;
*/

CREATE TABLE IF NOT EXISTS users (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    username TEXT NOT NULL,
    password TEXT NOT NULL
);

create table languages (
  id bigint generated always as identity primary key,
  code text not null unique,
  name text not null unique,
  hl7_code text,
  active boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table pharmacies (
  id bigint generated always as identity primary key,
  name text not null,
  address_line1 text,
  address_line2 text,
  city text,
  state text,
  postal_code text,
  phone text,
  fax_number text,
  area text,
  external_identifier text unique,
  active boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table patients (
  id bigint generated always as identity primary key,

  first_name text not null,
  middle_name text,
  last_name text not null,
  suffix text,
  nickname text,

  date_of_birth date not null,
  sex_at_birth text not null check (sex_at_birth in ('male', 'female', 'unknown')),
  gender_identity text,
  pronouns text,

  marital_status text,
  employment_status text,
  preferred_language_id bigint references languages(id),
  ethnicity text,

  status text not null default 'active',
  billing_status text,
  classification text,
  category text,
  stage text,
  reminder text,

  primary_provider_id bigint,
  primary_location_id bigint,

  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table patient_pharmacies (
  id bigint generated always as identity primary key,
  patient_id bigint not null references patients(id) on delete cascade,
  pharmacy_id bigint not null references pharmacies(id),
  type text not null check (type in ('primary', 'secondary', 'mail_order')),
  priority smallint not null,
  active boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),

  unique (patient_id, priority),
  unique (patient_id, type)
);

create table patient_contacts (
  id bigint generated always as identity primary key,
  patient_id bigint not null references patients(id) on delete cascade,

  address_line1 text,
  address_line2 text,
  city text,
  state text,
  postal_code text,
  country text default 'US',

  home_phone text,
  work_phone text,
  mobile_phone text,
  email text,
  communication_preference text,

  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table patient_notes (
  id bigint generated always as identity primary key,
  patient_id bigint not null references patients(id) on delete cascade,
  note_type text not null default 'general',
  body text not null,
  created_at timestamptz not null default now()
);

create table patient_recent_views (
  id bigint generated always as identity primary key,
  username text not null,
  patient_id bigint not null references patients(id) on delete cascade,
  viewed_at timestamptz not null default now(),

  unique (username, patient_id)
);

create index patient_recent_views_username_viewed_idx on patient_recent_views(username, viewed_at desc);

create table patient_cases (
  id bigint generated always as identity primary key,
  patient_id bigint not null references patients(id) on delete cascade,
  name text not null default 'Treatment',
  status text not null default 'active',
  start_date date not null default current_date,
  end_date date,
  authorization_number text,
  authorization_limit numeric(10,2),
  authorization_used numeric(10,2) default 0
);

create table insurance_carriers (
  id bigint generated always as identity primary key,
  name text not null,
  payer_id text,
  phone text,
  email text,
  address_line1 text,
  address_line2 text,
  city text,
  state text,
  postal_code text,
  created_at timestamptz not null default now()
);

create table patient_insurance_policies (
  id bigint generated always as identity primary key,
  patient_id bigint not null references patients(id) on delete cascade,
  carrier_id bigint references insurance_carriers(id),

  priority smallint not null default 1,
  member_id text,
  group_number text,
  group_name text,

  subscriber_first_name text,
  subscriber_middle_name text,
  subscriber_last_name text,
  subscriber_date_of_birth date,
  subscriber_sex_at_birth text,
  relationship_to_patient text,

  effective_date date,
  expiration_date date,
  copay numeric(10,2),
  is_active boolean not null default true,

  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),

  unique (patient_id, priority)
);

-- Optional support tables. Skip these if your app already has them.
create table providers (
  id bigint generated always as identity primary key,
  name text not null,
  role text,
  active boolean not null default true
);

create table locations (
  id bigint generated always as identity primary key,
  name text not null,
  active boolean not null default true
);

create table rooms (
  id bigint generated always as identity primary key,
  location_id bigint references locations(id),
  name text not null,
  active boolean not null default true
);

-- Schedule
create table appointment_types (
  id bigint generated always as identity primary key,
  name text not null,
  default_duration_minutes integer not null default 15,
  visit_type text,
  category text default 'office',
  color text,
  billable boolean not null default true,
  requires_clinical_note boolean not null default true,
  active boolean not null default true
);

create table provider_availability (
  id bigint generated always as identity primary key,
  provider_id bigint references providers(id),
  location_id bigint references locations(id),
  room_id bigint references rooms(id),
  valid_from date not null,
  valid_to date,
  days_of_week smallint[] not null, -- 0 Sunday through 6 Saturday
  start_time time not null,
  end_time time not null,
  slot_minutes integer not null default 15,
  is_closed boolean not null default false,
  portal_bookable boolean not null default false
);

create table schedule_blocks (
  id bigint generated always as identity primary key,
  availability_id bigint not null references provider_availability(id) on delete cascade,
  appointment_type_id bigint references appointment_types(id),
  start_time time not null,
  end_time time not null,
  label text,
  is_locked boolean not null default false
);

create table appointments (
  id bigint generated always as identity primary key,
  patient_id bigint references patients(id),
  appointment_type_id bigint references appointment_types(id),
  provider_id bigint references providers(id),
  location_id bigint references locations(id),
  room_id bigint references rooms(id),

  scheduled_start timestamptz not null,
  scheduled_end timestamptz not null,

  status text not null default 'scheduled' check (
    status in (
      'scheduled','confirmed','checked_in','triage','with_provider',
      'nurse_order','ready_checkout','checked_out','completed',
      'cancelled','no_show'
    )
  ),

  reason text,
  notes text,

  confirmed_at timestamptz,
  checked_in_at timestamptz,
  triaged_at timestamptz,
  provider_started_at timestamptz,
  encounter_closed_at timestamptz,
  checked_out_at timestamptz,
  signed_at timestamptz,

  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table appointment_status_history (
  id bigint generated always as identity primary key,
  appointment_id bigint not null references appointments(id) on delete cascade,
  from_status text,
  to_status text not null,
  note text,
  changed_at timestamptz not null default now()
);

create index appointments_scheduled_start_idx on appointments(scheduled_start);
create index appointments_provider_start_idx on appointments(provider_id, scheduled_start);
create index appointments_location_start_idx on appointments(location_id, scheduled_start);
create index appointments_room_start_idx on appointments(room_id, scheduled_start);
create index appointments_patient_start_idx on appointments(patient_id, scheduled_start desc);
create index appointment_status_history_appointment_idx on appointment_status_history(appointment_id, changed_at desc);

-- Clinical
create table visits (
  id bigint generated always as identity primary key,
  patient_id bigint not null references patients(id),
  appointment_id bigint unique references appointments(id),
  provider_id bigint references providers(id),
  nurse_id bigint references providers(id),
  location_id bigint references locations(id),

  visit_date date not null default current_date,
  visit_type text,
  status text not null default 'open' check (
    status in ('open','in_triage','with_provider','closed','signed','cancelled')
  ),

  chief_complaint text,
  smoking_status text,
  closed_at timestamptz,
  signed_at timestamptz,
  created_at timestamptz not null default now()
);

create table vital_signs (
  id bigint generated always as identity primary key,
  visit_id bigint not null references visits(id) on delete cascade,
  systolic_bp numeric(5,1),
  diastolic_bp numeric(5,1),
  heart_rate numeric(5,1),
  respiratory_rate numeric(5,1),
  temperature_c numeric(5,2),
  pulse_ox numeric(5,2),
  height_cm numeric(6,2),
  weight_kg numeric(6,2),
  bmi numeric(5,2),
  pain_score smallint,
  recorded_at timestamptz not null default now()
);

create table encounter_form_submissions (
  id bigint generated always as identity primary key,
  visit_id bigint not null references visits(id) on delete cascade,
  form_code text not null,
  section text,
  data jsonb not null default '{}'::jsonb,
  completed boolean not null default false,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table clinical_notes (
  id bigint generated always as identity primary key,
  visit_id bigint references visits(id) on delete cascade,
  patient_id bigint not null references patients(id),
  note_type text not null default 'visit',
  title text,
  body text not null,
  status text not null default 'draft' check (status in ('draft','signed','cosigned','voided')),
  signed_at timestamptz,
  created_at timestamptz not null default now()
);

create table patient_problems (
  id bigint generated always as identity primary key,
  patient_id bigint not null references patients(id),
  visit_id bigint references visits(id),
  diagnosis_code text,
  description text not null,
  status text not null default 'active' check (status in ('active','resolved','inactive')),
  onset_date date,
  resolved_date date,
  note text,
  created_at timestamptz not null default now()
);

create table visit_diagnoses (
  id bigint generated always as identity primary key,
  visit_id bigint not null references visits(id) on delete cascade,
  patient_problem_id bigint references patient_problems(id),
  sequence smallint not null check (sequence > 0),
  diagnosis_code text,
  description text,
  created_at timestamptz not null default now(),

  unique (visit_id, sequence)
);

create index visit_diagnoses_visit_idx on visit_diagnoses(visit_id, sequence);
create index visit_diagnoses_problem_idx on visit_diagnoses(patient_problem_id);

create table patient_allergies (
  id bigint generated always as identity primary key,
  patient_id bigint not null references patients(id),
  allergen text not null,
  allergen_type text,
  reaction text,
  severity text,
  status text not null default 'active' check (status in ('active','inactive','entered_in_error')),
  note text,
  created_at timestamptz not null default now()
);

create table patient_medications (
  id bigint generated always as identity primary key,
  patient_id bigint not null references patients(id),
  visit_id bigint references visits(id),
  medication_name text not null,
  strength text,
  dose text,
  route text,
  frequency text,
  start_date date,
  end_date date,
  refills integer,
  controlled boolean not null default false,
  status text not null default 'active' check (status in ('active','stopped','completed','voided')),
  instructions text,
  note text,
  created_at timestamptz not null default now()
);

create table clinical_orders (
  id bigint generated always as identity primary key,
  patient_id bigint not null references patients(id),
  visit_id bigint references visits(id),
  ordered_by_provider_id bigint references providers(id),
  order_type text not null check (order_type in ('lab','imaging','referral','procedure','medication','other')),
  code text,
  description text not null,
  diagnosis_code text,
  priority text default 'routine',
  status text not null default 'ordered' check (
    status in ('ordered','sent','resulted','completed','cancelled')
  ),
  ordered_at timestamptz not null default now(),
  completed_at timestamptz,
  note text
);

create table order_results (
  id bigint generated always as identity primary key,
  order_id bigint not null references clinical_orders(id) on delete cascade,
  result_status text default 'final',
  result_text text,
  result_data jsonb not null default '{}'::jsonb,
  resulted_at timestamptz not null default now()
);

-- Billing
create sequence if not exists billing_claim_number_seq;

create table billing_claims (
  id bigint generated always as identity primary key,
  claim_number text not null unique default ('CLM-' || lpad(nextval('billing_claim_number_seq')::text, 8, '0')),

  patient_id bigint not null references patients(id),
  visit_id bigint references visits(id),
  appointment_id bigint references appointments(id),
  insurance_policy_id bigint references patient_insurance_policies(id),
  provider_id bigint references providers(id),
  location_id bigint references locations(id),

  service_date date not null,
  status text not null default 'draft' check (
    status in ('draft','ready_to_bill','submitted','paid','denied','voided')
  ),
  billing_stage text not null default 'charge_entry' check (
    billing_stage in ('charge_entry','coding_review','ready_to_bill','submitted','follow_up','closed')
  ),

  total_charge numeric(12,2) not null default 0,
  total_allowed numeric(12,2) not null default 0,
  total_paid numeric(12,2) not null default 0,
  total_adjustment numeric(12,2) not null default 0,
  insurance_balance numeric(12,2) not null default 0,
  patient_balance numeric(12,2) not null default 0,
  note text,

  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table billing_claim_diagnoses (
  id bigint generated always as identity primary key,
  claim_id bigint not null references billing_claims(id) on delete cascade,
  sequence smallint not null check (sequence > 0),
  diagnosis_code text not null,
  description text,

  unique (claim_id, sequence)
);

create table billing_claim_lines (
  id bigint generated always as identity primary key,
  claim_id bigint not null references billing_claims(id) on delete cascade,
  service_date date not null,
  procedure_code text not null,
  description text not null,
  units numeric(8,2) not null default 1 check (units > 0),
  charge_amount numeric(12,2) not null default 0 check (charge_amount >= 0),
  allowed_amount numeric(12,2) not null default 0,
  paid_amount numeric(12,2) not null default 0,
  adjustment_amount numeric(12,2) not null default 0,
  patient_responsibility_amount numeric(12,2) not null default 0,
  insurance_balance numeric(12,2) not null default 0,
  patient_balance numeric(12,2) not null default 0,
  diagnosis_pointer text,
  rendering_provider_id bigint references providers(id),
  created_at timestamptz not null default now()
);

create table billing_claim_events (
  id bigint generated always as identity primary key,
  claim_id bigint not null references billing_claims(id) on delete cascade,
  event_type text not null,
  from_status text,
  to_status text,
  note text,
  created_by_user_id bigint references users(id),
  created_at timestamptz not null default now()
);

create index billing_claims_patient_idx on billing_claims(patient_id);
create index billing_claims_service_date_idx on billing_claims(service_date);
create index billing_claims_status_stage_idx on billing_claims(status, billing_stage);
create index billing_claim_lines_claim_idx on billing_claim_lines(claim_id);
create index billing_claim_events_claim_idx on billing_claim_events(claim_id, created_at desc);
