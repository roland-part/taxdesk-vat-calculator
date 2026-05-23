import type { VatReport, VatSection } from '../types/vatReport';

interface Props {
  report: VatReport;
  onDownloadPdf: () => void;
  isPdfLoading: boolean;
}

export function VatReportTable({ report, onDownloadPdf, isPdfLoading }: Props) {
  return (
    <div className="report">
      <div className="report-header">
        <div>
          <h2>VAT Declaration Report</h2>
          <p className="report-meta">
            <span>{report.fileName}</span>
            <span>·</span>
            <span>{report.totalInvoices} invoices</span>
            <span>·</span>
            <span>Generated {new Date(report.generatedAt).toLocaleString()}</span>
          </p>
        </div>
        <button
          className="btn-pdf"
          onClick={onDownloadPdf}
          disabled={isPdfLoading}
        >
          {isPdfLoading ? 'Generating…' : '⬇ Download PDF'}
        </button>
      </div>

      <Section section={report.sales} title="Sales — Output VAT (Fizetendő ÁFA)" accent="blue" />
      <Section section={report.purchases} title="Purchases — Input VAT (Levonható ÁFA)" accent="green" />
    </div>
  );
}

function Section({ section, title, accent }: { section: VatSection; title: string; accent: string }) {
  const fmt = (n: number) =>
    new Intl.NumberFormat('hu-HU', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(n);

  return (
    <div className={`section section-${accent}`}>
      <h3>{title}</h3>
      {section.lines.length === 0 ? (
        <p className="no-data">No transactions in this period.</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>VAT Rate</th>
              <th className="num">Invoices</th>
              <th className="num">Net Amount</th>
              <th className="num">VAT Amount</th>
              <th className="num">Gross Amount</th>
            </tr>
          </thead>
          <tbody>
            {section.lines.map((line) => (
              <tr key={line.vatRate}>
                <td>{line.vatRate === 'AAM' ? 'AAM (exempt)' : `${line.vatRate}%`}</td>
                <td className="num">{line.invoiceCount}</td>
                <td className="num">{fmt(line.totalNet)}</td>
                <td className="num">{fmt(line.totalVat)}</td>
                <td className="num">{fmt(line.totalGross)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="total-row">
              <td>Total</td>
              <td className="num">{section.lines.reduce((s, l) => s + l.invoiceCount, 0)}</td>
              <td className="num">{fmt(section.grandTotalNet)}</td>
              <td className="num">{fmt(section.grandTotalVat)}</td>
              <td className="num">{fmt(section.grandTotalGross)}</td>
            </tr>
          </tfoot>
        </table>
      )}
    </div>
  );
}
