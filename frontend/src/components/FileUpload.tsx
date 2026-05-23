import { useRef, useState } from 'react';

interface Props {
  onUpload: (file: File) => void;
  isLoading: boolean;
}

const MAX_SIZE_MB = 10;

export function FileUpload({ onUpload, isLoading }: Props) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragOver, setDragOver] = useState(false);
  const [clientError, setClientError] = useState<string | null>(null);

  function validate(file: File): string | null {
    if (!file.name.toLowerCase().endsWith('.csv'))
      return 'Only .csv files are accepted.';
    if (file.size > MAX_SIZE_MB * 1024 * 1024)
      return `File must be smaller than ${MAX_SIZE_MB} MB.`;
    return null;
  }

  function handleFile(file: File) {
    const err = validate(file);
    if (err) { setClientError(err); return; }
    setClientError(null);
    onUpload(file);
  }

  function handleChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (file) handleFile(file);
    e.target.value = '';
  }

  function handleDrop(e: React.DragEvent) {
    e.preventDefault();
    setDragOver(false);
    const file = e.dataTransfer.files[0];
    if (file) handleFile(file);
  }

  return (
    <div className="upload-area">
      <div
        className={`dropzone${dragOver ? ' dragover' : ''}`}
        onClick={() => inputRef.current?.click()}
        onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
        onDragLeave={() => setDragOver(false)}
        onDrop={handleDrop}
        role="button"
        tabIndex={0}
        onKeyDown={(e) => e.key === 'Enter' && inputRef.current?.click()}
        aria-label="Upload CSV file"
      >
        <span className="dropzone-icon">📂</span>
        <p><strong>Click to select</strong> or drag &amp; drop a CSV file here</p>
        <p className="dropzone-hint">Max {MAX_SIZE_MB} MB · .csv only</p>
        {isLoading && <p className="uploading">Processing…</p>}
      </div>

      {clientError && <p className="error-msg">{clientError}</p>}

      <input
        ref={inputRef}
        type="file"
        accept=".csv"
        style={{ display: 'none' }}
        onChange={handleChange}
      />
    </div>
  );
}
