import './invoice.css'
import { amountInWords } from './amountInWords'

export type InvoiceBiz = { businessName: string; address: string; city: string; state: string; pinCode: string; mobile: string; gstin: string | null }
export type InvoiceItem = { name: string; quantity: number; unitPrice: number; discount: number; serials?: string[]; imei1s?: string[]; imei2s?: string[]; model?: string; barcode?: string; hsn?: string }

const money = (n: number) => n.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const itemTotal = (i: InvoiceItem) => Math.round(i.quantity * i.unitPrice * 100) / 100 - i.discount

export function InvoiceDocument({ biz, showGst, rate, invoiceNo, date, payment, location, customerName, customerPhone, customerAddress, items, paid, credit }: {
  biz: InvoiceBiz | null; showGst: boolean; rate: number; invoiceNo: string; date: string; payment: string; location?: string
  customerName: string; customerPhone?: string | null; customerAddress?: string; items: InvoiceItem[]; paid: number; credit: boolean
}) {
  const total = items.reduce((s, i) => s + itemTotal(i), 0)
  const taxable = showGst ? total / (1 + rate / 100) : total
  const tax = total - taxable
  const bizAddress = biz ? [biz.address, biz.city, biz.state, biz.pinCode].filter(Boolean).join(', ') : ''
  return (
    <article className="invoice">
      <header className="invoice-head">
        <div>
          <h2>{biz?.businessName ?? 'Business'}</h2>
          {bizAddress && <p>{bizAddress}</p>}
          {biz?.mobile && <p>Phone: {biz.mobile}</p>}
          {showGst && biz?.gstin && <p>GSTIN: {biz.gstin}</p>}
        </div>
        <div className="invoice-title">{showGst ? 'TAX INVOICE' : 'INVOICE'}</div>
      </header>
      <div className="invoice-meta invoice-meta-row">
        <div><span>Invoice no.</span><strong>{invoiceNo}</strong></div>
        <div><span>Date</span><strong>{date}</strong></div>
        <div><span>Payment</span><strong>{payment}</strong></div>
        {location && <div><span>Sold from</span><strong>{location}</strong></div>}
      </div>
      <div className="invoice-party">
        <div><h4>Seller</h4><strong>{biz?.businessName ?? 'Business'}</strong>{bizAddress && <p>{bizAddress}</p>}{biz?.mobile && <p>Phone: {biz.mobile}</p>}{showGst && biz?.gstin && <p>GSTIN: {biz.gstin}</p>}</div>
        <div><h4>Bill to</h4><strong>{customerName || 'Walk-in customer'}</strong>{customerPhone && <p>Phone: {customerPhone}</p>}{customerAddress && <p>{customerAddress}</p>}</div>
      </div>
      <table className="invoice-lines">
        <thead><tr><th>#</th><th>Item</th><th>Qty</th><th>Rate</th><th>Discount</th>{showGst && <><th>Taxable</th><th>CGST</th><th>SGST</th></>}<th>Amount</th></tr></thead>
        <tbody>{items.map((i, n) => { const t = itemTotal(i); const tv = t / (1 + rate / 100); const half = (t - tv) / 2; return <tr key={n}><td>{n + 1}</td><td>{i.name}{i.model && <small>Model: {i.model}</small>}{showGst && i.hsn && <small>HSN: {i.hsn}</small>}{i.serials && i.serials.length > 0 && <small>Serial: {i.serials.join(', ')}</small>}{i.imei1s && i.imei1s.length > 0 && <small>IMEI 1: {i.imei1s.join(', ')}</small>}{i.imei2s && i.imei2s.length > 0 && <small>IMEI 2: {i.imei2s.join(', ')}</small>}{i.barcode && <small>Barcode: {i.barcode}</small>}</td><td>{i.quantity}</td><td>{money(i.unitPrice)}</td><td>{money(i.discount)}</td>{showGst && <><td>{money(tv)}</td><td>{money(half)}<small>{rate / 2}%</small></td><td>{money(half)}<small>{rate / 2}%</small></td></>}<td>{money(t)}</td></tr> })}</tbody>
        <tfoot><tr className="invoice-total-row"><td colSpan={5}>Total</td>{showGst && <><td>{money(taxable)}</td><td>{money(tax / 2)}</td><td>{money(tax / 2)}</td></>}<td>₹{money(total)}</td></tr></tfoot>
      </table>
      </table>
      <div className="invoice-mobile-lines" aria-label="Invoice items">
        {items.map((i, n) => <section className="invoice-mobile-item" key={n}>
          <div className="invoice-mobile-item-heading"><strong>{n + 1}. {i.name}</strong><strong>₹{money(itemTotal(i))}</strong></div>
          {i.model && <small>Model: {i.model}</small>}
          {showGst && i.hsn && <small>HSN: {i.hsn}</small>}
          {i.serials?.length ? <small>Serial: {i.serials.join(', ')}</small> : null}
          {i.imei1s?.length ? <small>IMEI 1: {i.imei1s.join(', ')}</small> : null}
          {i.imei2s?.length ? <small>IMEI 2: {i.imei2s.join(', ')}</small> : null}
          {i.barcode && <small>Barcode: {i.barcode}</small>}
          <dl><dt>Qty</dt><dd>{i.quantity}</dd><dt>Rate</dt><dd>₹{money(i.unitPrice)}</dd><dt>Discount</dt><dd>₹{money(i.discount)}</dd>
            {showGst && <><dt>Taxable</dt><dd>₹{money(itemTotal(i) / (1 + rate / 100))}</dd><dt>CGST</dt><dd>₹{money((itemTotal(i) - itemTotal(i) / (1 + rate / 100)) / 2)}</dd><dt>SGST</dt><dd>₹{money((itemTotal(i) - itemTotal(i) / (1 + rate / 100)) / 2)}</dd></>}
          </dl>
        </section>)}
        <div className="invoice-mobile-total"><strong>Total</strong><strong>₹{money(total)}</strong></div>
      </div>
<div className="invoice-foot">
        <div className="invoice-note"><h4>Amount in words</h4><p className="invoice-words">{amountInWords(total)}</p></div>
        {credit && <dl className="invoice-totals"><dt>Paid</dt><dd>₹{money(paid)}</dd><dt className="strong">Balance due</dt><dd className="strong">₹{money(total - paid)}</dd></dl>}
      </div>
      <div className="invoice-terms"><h4>Terms &amp; conditions</h4><p>Goods once sold will not be taken back. Prices are inclusive of all taxes.</p></div>
      <div className="invoice-sign"><span>Customer signature</span><span>For {biz?.businessName ?? 'Business'}<br />Authorised signatory</span></div>
    </article>
  )
}
