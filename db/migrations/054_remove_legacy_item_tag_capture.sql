-- 054_remove_legacy_item_tag_capture.sql
-- The invoice scanner now processes images synchronously in memory. Remove the
-- obsolete asynchronous queue and private photo-evidence store; retain migrations
-- 049/050 as immutable history and perform cleanup through this forward migration.
drop function if exists integrations.enqueue_item_tag_capture(uuid, uuid, uuid);
drop function if exists integrations.claim_item_tag_captures(integer, integer);
drop function if exists integrations.finish_item_tag_capture(uuid, uuid, timestamptz);
drop function if exists integrations.cancel_item_tag_capture(uuid, uuid, uuid);

drop table if exists integrations.item_tag_capture_queue;
drop table if exists sales.item_tag_capture;
