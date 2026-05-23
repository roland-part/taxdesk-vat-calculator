import { useState } from 'react';
import { UploadForm } from './components/UploadForm';
import { VatReportTable } from './components/VatReportTable';
import { uploadCsv, downloadPdf, ApiError, type UploadParams } from './api/vatApi';
import type { VatReport } from './types/vatReport';
import './App.css';

type AppState = 'idle' | 'uploading' | 'done' | 'error';

export default function App() {
  const [state, setState] = useState<AppState>('idle');
  const [report, setReport] = useState<VatReport | null>(null);
  const [errors, setErrors] = useState<string[]>([]);
  const [isPdfLoading, setIsPdfLoading] = useState(false);

  async function handleUpload(params: UploadParams) {
    setState('uploading');
    setErrors([]);
    setReport(null);

    try {
      const result = await uploadCsv(params);
      setReport(result);
      setState('done');
    } catch (err) {
      if (err instanceof ApiError && err.fieldErrors?.['csv']) {
        setErrors(err.fieldErrors['csv']);
      } else if (err instanceof Error) {
        setErrors([err.message]);
      } else {
        setErrors(['An unexpected error occurred.']);
      }
      setState('error');
    }
  }

  async function handleDownloadPdf() {
    if (!report) return;
    setIsPdfLoading(true);
    try {
      await downloadPdf(report);
    } catch {
      setErrors(['Failed to generate PDF. Please try again.']);
    } finally {
      setIsPdfLoading(false);
    }
  }

  function handleReset() {
    setState('idle');
    setReport(null);
    setErrors([]);
  }

  return (
    <div className="app">
      <header className="app-header">
        <h1>Hungarian VAT Declaration Generator</h1>
        <p>ÁFA Bevallás Összesítő</p>
      </header>

      <main className="app-main">
        {state !== 'done' && (
          <UploadForm onSubmit={handleUpload} isLoading={state === 'uploading'} />
        )}

        {state === 'error' && errors.length > 0 && (
          <div className="error-box">
            <h3>Could not process the file</h3>
            <ul>
              {errors.map((e, i) => <li key={i}>{e}</li>)}
            </ul>
            <button className="btn-secondary" onClick={handleReset}>Try again</button>
          </div>
        )}

        {state === 'done' && report && (
          <>
            <div className="actions-row">
              <button className="btn-action" onClick={handleDownloadPdf} disabled={isPdfLoading}>
                {isPdfLoading ? 'Generating…' : '⬇ Download PDF'}
              </button>
              <button className="btn-action" onClick={handleReset}>
                ↑ Upload another file
              </button>
            </div>
            <VatReportTable report={report} />
          </>
        )}
      </main>
    </div>
  );
}
