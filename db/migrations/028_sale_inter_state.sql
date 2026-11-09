-- A1: inter-state (IGST) support. place_of_supply is the customer's state; is_inter_state is derived at posting time.
alter table sales.sale
  add column place_of_supply text null check (place_of_supply is null or length(btrim(place_of_supply)) between 1 and 80),
  add column is_inter_state boolean not null default false;
