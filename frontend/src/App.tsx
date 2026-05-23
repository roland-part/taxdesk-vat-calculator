import { useState } from 'react';
import { FileUpload } from './components/FileUpload';
import { VatReportTable } from './components/VatReportTable';
import { uploadCsv, downloadPdf, ApiError } from './api/vatApi';
import type { VatReport } from './types/vatReport';
import './App.css';

type AppState = 'idle' | 'uploading' | 'done' | 'error';

export default function App() {
  const [state, setState] = useState<AppState>('idle');
  const [report, setReport] = useState<VatReport | null>(null);
  const [errors, setErrors] = useState<string[]>([]);
  const [isPdfLoading, setIsPdfLoading] = useState(false);

  async function handleUpload(file: File) {
    setState('uploading');
    setErrors([]);
    setReport(null);

    try {
      const result = await uploadCsv(file);
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
          <FileUpload onUpload={handleUpload} isLoading={state === 'uploading'} />
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
            <VatReportTable
              report={report}
              onDownloadPdf={handleDownloadPdf}
              isPdfLoading={isPdfLoading}
            />
            <div className="reset-row">
              <button className="btn-secondary" onClick={handleReset}>
                Upload another file
              </button>
            </div>
          </>
        )}
      </main>
    </div>
  );
}
