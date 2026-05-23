import { useRef, useState } from 'react';
import type { UploadParams } from '../api/vatApi';

interface Props {
  onSubmit: (params: UploadParams) => void;
  isLoading: boolean;
}

type PeriodType = 'monthly' | 'quarterly';

const MONTHS = [
  'January','February','March','April','May','June',
  'July','August','September','October','November','December',
];
const QUARTERS = ['Q1 (Jan–Mar)', 'Q2 (Apr–Jun)', 'Q3 (Jul–Sep)', 'Q4 (Oct–Dec)'];
const MAX_SIZE_MB = 10;
const currentYear = new Date().getFullYear();
const YEARS = Array.from({ length: 5 }, (_, i) => currentYear - i);

function buildPeriod(type: PeriodType, year: number, month: number, quarter: number): string {
  if (type === 'monthly') return `${year}-${String(month).padStart(2, '0')}`;
  return `${year}-Q${quarter}`;
}

export function UploadForm({ onSubmit, isLoading }: Props) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [dragOver, setDragOver] = useState(false);
  const [periodType, setPeriodType] = useState<PeriodType>('monthly');
  const [year, setYear] = useState(currentYear);
  const [month, setMonth] = useState(new Date().getMonth() + 1);
  const [quarter, setQuarter] = useState(Math.ceil((new Date().getMonth() + 1) / 3));
  const [taxpayerName, setTaxpayerName] = useState('');
  const [taxpayerTaxNumber, setTaxpayerTaxNumber] = useState('');
  const [fileError, setFileError] = useState<string | null>(null);

  function validateFile(f: File): string | null {
    if (!f.name.toLowerCase().endsWith('.csv')) return 'Only .csv files are accepted.';
    if (f.size > MAX_SIZE_MB * 1024 * 1024) return `File must be smaller than ${MAX_SIZE_MB} MB.`;
    return null;
  }

  function handleFile(f: File) {
    const err = validateFile(f);
    setFileError(err);
    if (!err) setFile(f);
  }

  function handleDrop(e: React.DragEvent) {
    e.preventDefault();
    setDragOver(false);
    const f = e.dataTransfer.files[0];
    if (f) handleFile(f);
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!file || fileError) return;
    onSubmit({
      file,
      period: buildPeriod(periodType, year, month, quarter),
      taxpayerName:      taxpayerName.trim() || undefined,
      taxpayerTaxNumber: taxpayerTaxNumber.trim() || undefined,
    });
  }

  return (
    <form className="upload-form" onSubmit={handleSubmit}>
      {/* ── Period ─────────────────────────────────────────── */}
      <fieldset className="form-section">
        <legend>Declaration Period</legend>

        <div className="period-toggle">
          {(['monthly', 'quarterly'] as PeriodType[]).map(t => (
            <button
              key={t}
              type="button"
              className={`toggle-btn${periodType === t ? ' active' : ''}`}
              onClick={() => setPeriodType(t)}
            >
              {t === 'monthly' ? 'Monthly' : 'Quarterly'}
            </button>
          ))}
        </div>

        <div className="period-selectors">
          <div className="field">
            <label>Year</label>
            <select value={year} onChange={e => setYear(Number(e.target.value))}>
              {YEARS.map(y => <option key={y} value={y}>{y}</option>)}
            </select>
          </div>

          {periodType === 'monthly' ? (
            <div className="field">
              <label>Month</label>
              <select value={month} onChange={e => setMonth(Number(e.target.value))}>
                {MONTHS.map((m, i) => <option key={i + 1} value={i + 1}>{m}</option>)}
              </select>
            </div>
          ) : (
            <div className="field">
              <label>Quarter</label>
              <select value={quarter} onChange={e => setQuarter(Number(e.target.value))}>
                {QUARTERS.map((q, i) => <option key={i + 1} value={i + 1}>{q}</option>)}
              </select>
            </div>
          )}
        </div>
      </fieldset>

      {/* ── Taxpayer info (optional) ────────────────────────── */}
      <fieldset className="form-section">
        <legend>Taxpayer Information <span className="optional">(optional)</span></legend>
        <div className="field-row">
          <div className="field">
            <label>Company / Taxpayer Name</label>
            <input
              type="text"
              value={taxpayerName}
              onChange={e => setTaxpayerName(e.target.value)}
              placeholder="e.g. Acme Kft"
              maxLength={200}
            />
          </div>
          <div className="field">
            <label>Tax Number (Adószám)</label>
            <input
              type="text"
              value={taxpayerTaxNumber}
              onChange={e => setTaxpayerTaxNumber(e.target.value)}
              placeholder="e.g. 12345678-2-41"
              maxLength={20}
            />
          </div>
        </div>
      </fieldset>

      {/* ── File upload ─────────────────────────────────────── */}
      <fieldset className="form-section">
        <legend>Invoice CSV File</legend>
        <div
          className={`dropzone${dragOver ? ' dragover' : ''}${file ? ' has-file' : ''}`}
          onClick={() => inputRef.current?.click()}
          onDragOver={e => { e.preventDefault(); setDragOver(true); }}
          onDragLeave={() => setDragOver(false)}
          onDrop={handleDrop}
          role="button"
          tabIndex={0}
          onKeyDown={e => e.key === 'Enter' && inputRef.current?.click()}
          aria-label="Upload CSV file"
        >
          <span className="dropzone-icon">{file ? '✅' : '📂'}</span>
          {file
            ? <p><strong>{file.name}</strong> ({(file.size / 1024).toFixed(1)} KB)</p>
            : <p><strong>Click to select</strong> or drag &amp; drop</p>
          }
          <p className="dropzone-hint">Max {MAX_SIZE_MB} MB · .csv only</p>
        </div>
        {fileError && <p className="field-error">{fileError}</p>}
        <input ref={inputRef} type="file" accept=".csv" style={{ display: 'none' }}
          onChange={e => { const f = e.target.files?.[0]; if (f) handleFile(f); e.target.value = ''; }} />
      </fieldset>

      <button
        type="submit"
        className="btn-submit"
        disabled={!file || !!fileError || isLoading}
      >
        {isLoading ? 'Processing…' : 'Generate VAT Report'}
      </button>
    </form>
  );
}
