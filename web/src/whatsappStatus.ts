export function whatsappStatusLabel(status: string): string {
  switch (status) {
    case 'Pending': return 'Queued';
    case 'Processing': return 'Sending';
    case 'Retry': return 'Retrying';
    case 'Accepted': return 'Accepted by WhatsApp';
    case 'Sent': return 'Sent';
    case 'Delivered': return 'Delivered';
    case 'Read': return 'Read';
    case 'Failed': return 'Failed';
    case 'WaitingForSetup': return 'Setup required';
    case 'NoRecipient': return 'No phone number';
    case 'ConsentRequired': return 'Customer consent needed';
    case 'Suppressed': return 'Customer opted out';
    case 'NeedsReview': return 'Check WhatsApp before retry';
    default: return status === 'NotAvailable' ? 'Not available' : status;
  }
}


export function whatsappStatusDescription(status: string): string {
  if (status === 'ConsentRequired') return 'Ask the customer for permission before sending this bill on WhatsApp.'
  if (status === 'NeedsReview') return 'Check the previous send in WhatsApp before retrying to avoid a duplicate bill.'
  return ''
}
