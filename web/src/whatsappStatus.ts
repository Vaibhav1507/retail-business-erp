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
    case 'ConsentRequired': return 'Permission not confirmed';
    case 'Suppressed': return 'Customer opted out';
    case 'NeedsReview': return 'Check WhatsApp before retry';
    default: return status === 'NotAvailable' ? 'Not available' : status;
  }
}
