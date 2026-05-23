import type { VatReport, VatSection, VatCategoryLine } from '../types/vatReport';

interface Props {
  report: VatReport;
}

function formatPeriod(period: string): string {
  const monthly = period.match(/^(\d{4})-(0[1-9]|1[0-2])$/);
  if (monthly) {
    const d = new Date(Number(monthly[1]), Number(monthly[2]) - 1, 1);
    return d.toLocaleString('en-GB', { month: 'long', year: 'numeric' });
  }
  const quarterly = period.match(/^(\d{4})-Q([1-4])$/);
  if (quarterly) {
    const labels: Record<string, string> = {
      '1': 'Jan–Mar', '2': 'Apr–Jun', '3': 'Jul–Sep', '4': 'Oct–Dec',
    };
    return `Q${quarterly[2]} ${quarterly[1]} (${labels[quarterly[2]]})`;
  }
  if (/^\d{4}$/.test(period)) return `${period} (Annual)`;
  return period;
}

export function VatReportTable({ report }: Props) {
  const fmt = (n: number) =>
    new Intl.NumberFormat('hu-HU', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(n);

  const vatPayable = report.vatPayable;
  const isPayable  = vatPayable >= 0;

  return (
    <div className="report">
      <div className="report-header">
        <h2>VAT Declaration Report</h2>
        <p className="report-meta">
          <span>{report.fileName}</span>
          <span>·</span>
          <span>{report.totalInvoices} invoices</span>
          <span>·</span>
          <span>Generated {new Date(report.generatedAt).toLocaleString()}</span>
        </p>
      </div>

      {/* ── Declaration metadata ── */}
      <div className="decl-meta">
        <div className="decl-meta-row">
          <span className="decl-label">Period</span>
          <span className="decl-value">{formatPeriod(report.period)}</span>
        </div>
        {report.taxpayerName && (
          <div className="decl-meta-row">
            <span className="decl-label">Taxpayer</span>
            <span className="decl-value">{report.taxpayerName}</span>
          </div>
        )}
        {report.taxpayerTaxNumber && (
          <div className="decl-meta-row">
            <span className="decl-label">Tax number</span>
            <span className="decl-value">{report.taxpayerTaxNumber}</span>
          </div>
        )}
      </div>

      {/* ── Warnings ── */}
      {report.warnings.length > 0 && (
        <div className="warnings-box">
          <strong>⚠ Warnings</strong>
          <ul>{report.warnings.map((w, i) => <li key={i}>{w}</li>)}</ul>
        </div>
      )}

      <Section section={report.sales}     title="Sales — Output VAT (Fizetendő ÁFA)"     accent="blue" />
      <Section section={report.purchases} title="Purchases — Input VAT (Levonható ÁFA)" accent="green" />

      {/* ── Net VAT position ── */}
      <div className="net-position">
        <h3>Net VAT Position</h3>
        <table>
          <thead>
            <tr>
              <th>Output VAT — HUF (Sales)</th>
              <th className="num">Input VAT — HUF (Purchases)</th>
              <th className="num">{isPayable ? 'Payable to NAV — HUF' : 'Refundable — HUF'}</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td>{fmt(report.sales.grandTotalVat)}</td>
              <td className="num">{fmt(report.purchases.grandTotalVat)}</td>
              <td className={`num net-amount ${isPayable ? 'payable' : 'refundable'}`}>
                {fmt(Math.abs(vatPayable))}
              </td>
            </tr>
          </tbody>
        </table>
        <p className={`net-label ${isPayable ? 'payable' : 'refundable'}`}>
          {isPayable
            ? `▲ HUF ${fmt(vatPayable)} payable to NAV`
            : `▼ HUF ${fmt(Math.abs(vatPayable))} refundable`}
        </p>
      </div>
    </div>
  );
}

function formatLineLabel(line: VatCategoryLine): string {
  const rateLabel = line.vatRate === 'AAM' ? 'AAM (exempt)' : `${line.vatRate}%`;
  switch (line.transactionType) {
    case 'Domestic':                  return rateLabel;
    case 'ReverseCharge':             return `Reverse charge §142 — ${rateLabel}`;
    case 'IntraCommunitySale':        return `Intra-Community supply — ${rateLabel}`;
    case 'IntraCommunityAcquisition': return `Intra-Community acquisition — ${rateLabel}`;
    case 'Import':                    return `Import — ${rateLabel}`;
    default:                          return `${line.transactionType} — ${rateLabel}`;
  }
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
              <th>Type / Rate</th>
              <th className="num">Invoices</th>
              <th className="num">Net (HUF)</th>
              <th className="num">VAT (HUF)</th>
              <th className="num">Gross (HUF)</th>
            </tr>
          </thead>
          <tbody>
            {section.lines.map((line) => (
              <tr key={`${line.transactionType}-${line.vatRate}`}>
                <td>{formatLineLabel(line)}</td>
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
