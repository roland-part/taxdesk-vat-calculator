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
  const [errorHeading, setErrorHeading] = useState<string>('Could not process the file');
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
      if (err instanceof ApiError) {
        // Collect every message from every field-error bucket, then fall back to the top-level message.
        const fieldMessages = err.fieldErrors
          ? Object.values(err.fieldErrors).flat()
          : [];
        setErrorHeading(err.message);
        setErrors(fieldMessages.length > 0 ? fieldMessages : []);
      } else if (err instanceof Error) {
        setErrorHeading('Could not process the file');
        setErrors([err.message]);
      } else {
        setErrorHeading('Could not process the file');
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
    setErrorHeading('Could not process the file');
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

        {state === 'error' && (
          <div className="error-box">
            <h3>{errorHeading}</h3>
            {errors.length > 0 && (
              <ul>{errors.map((e, i) => <li key={i}>{e}</li>)}</ul>
            )}
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
