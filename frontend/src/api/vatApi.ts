import type { VatReport } from '../types/vatReport';

const BASE_URL = `${import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5150'}/api/vat`;

export interface UploadParams {
  file: File;
  period: string;
  taxpayerName?: string;
  taxpayerTaxNumber?: string;
}

export async function uploadCsv(params: UploadParams): Promise<VatReport> {
  const form = new FormData();
  form.append('file', params.file);
  form.append('period', params.period);
  if (params.taxpayerName)      form.append('taxpayerName', params.taxpayerName);
  if (params.taxpayerTaxNumber) form.append('taxpayerTaxNumber', params.taxpayerTaxNumber);

  const res = await fetch(`${BASE_URL}/upload`, { method: 'POST', body: form });

  if (!res.ok) {
    const err = await res.json().catch(() => ({ message: 'Upload failed.' }));
    throw new ApiError(res.status, err.message, err.errors);
  }

  return res.json();
}

export async function downloadPdf(report: VatReport): Promise<void> {
  const res = await fetch(`${BASE_URL}/report/pdf`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(report),
  });

  if (!res.ok) {
    throw new ApiError(res.status, 'PDF generation failed.');
  }

  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `vat-report-${report.generatedAt.slice(0, 10)}.pdf`;
  a.click();
  URL.revokeObjectURL(url);
}

export class ApiError extends Error {
  readonly status: number;
  readonly fieldErrors?: Record<string, string[]>;

  constructor(status: number, message: string, fieldErrors?: Record<string, string[]>) {
    super(message);
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}
