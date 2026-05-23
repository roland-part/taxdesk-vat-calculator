export type TransactionDirection = 'Sale' | 'Purchase';

export type VatRateKey = '27' | '18' | '5' | '0' | 'AAM';

export interface VatCategoryLine {
  vatRate: string;
  totalNet: number;
  totalVat: number;
  totalGross: number;
  invoiceCount: number;
}

export interface VatSection {
  direction: TransactionDirection;
  lines: VatCategoryLine[];
  grandTotalNet: number;
  grandTotalVat: number;
  grandTotalGross: number;
}

export interface VatReport {
  generatedAt: string;
  fileName: string;
  totalInvoices: number;
  sales: VatSection;
  purchases: VatSection;
}

export interface ApiError {
  message: string;
  errors?: Record<string, string[]>;
}
