-- Multi-page invoices are stored as one zip of the page images/PDFs in the same invoice_file row.
alter table procurement.invoice_file drop constraint if exists invoice_file_content_type_check;
alter table procurement.invoice_file add constraint invoice_file_content_type_check
  check (content_type in ('image/jpeg','image/png','image/webp','application/pdf','application/zip'));
alter table procurement.invoice_file drop constraint if exists invoice_file_size_bytes_check;
alter table procurement.invoice_file add constraint invoice_file_size_bytes_check
  check (size_bytes between 1 and 12582912);
