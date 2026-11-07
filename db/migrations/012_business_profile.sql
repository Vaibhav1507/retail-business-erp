-- Business profile fields collected during one-time setup. Existing tenants keep their rows;
-- onboarding_completed defaults to false so owners are asked to finish setup.
alter table core.business_profile
  add column business_category text null check (business_category in ('electronics_mobile','electrical_hardware','general_retail','garments_apparel','automobile_spares','sports_fitness','pharmacy_medical','other_retail')),
  add column owner_name text null check (owner_name is null or length(btrim(owner_name)) between 1 and 120),
  add column mobile text null check (mobile is null or mobile ~ '^[6-9][0-9]{9}$'),
  add column mobile_verification_status text not null default 'Unverified' check (mobile_verification_status in ('Unverified','Verified')),
  add column address text null check (address is null or length(btrim(address)) between 1 and 250),
  add column city text null check (city is null or length(btrim(city)) between 1 and 80),
  add column state text null check (state is null or length(btrim(state)) between 1 and 80),
  add column pin_code text null check (pin_code is null or pin_code ~ '^[1-9][0-9]{5}$'),
  add column gst_status text null check (gst_status in ('NotRegistered','Regular','Composition','Undecided')),
  add column gstin text null check (gstin is null or gstin ~ '^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][A-Z0-9]Z[A-Z0-9]$'),
  add column financial_year_start_month smallint not null default 4 check (financial_year_start_month between 1 and 12),
  add column language text not null default 'en' check (language in ('en')),
  add column onboarding_completed boolean not null default false;

grant update on core.business_profile to rbos_app;
