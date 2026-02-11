export interface PartInfo {
  itemKey: string;
  partName: string;
  hebrewDescription: string | null;
  arabicDescription: string | null;
  category: string | null;
  localization: string | null;
}

export interface StockInfo {
  stockId: number;
  accountName: string;
  accountKey: string;
  valueDate: string | null;
  remarks: string;
}

export interface StockMoveItem {
  itemKey: string;
  totalQuantity: number;
  localization: string | null;
  originalOrder: number;
}

export interface PrinterInfo {
  name: string;
  status: number;
  statusMessage: string;
  printerType: string;
  isConnected: boolean;
}

export interface PrintProgress {
  current: number;
  total: number;
  currentItemKey: string;
  status: string;
}

export interface PrintComplete {
  totalPrinted: number;
  errors: string[];
}

export type Language = 'he' | 'ar';
export type SortOption = 'localization' | 'itemKey' | 'originalOrder';

export interface StockMoveDisplayItem extends StockMoveItem {
  language: Language;
  description: string;
  quantity: number;
}
