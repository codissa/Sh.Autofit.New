import type { PartInfo, StockInfo, StockMoveItem, PrinterInfo } from '../types';

const BASE = '/api';

async function fetchJson<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, init);
  if (!res.ok) {
    const body = await res.json().catch(() => ({ message: res.statusText }));
    throw new Error(body.message || res.statusText);
  }
  return res.json();
}

// Parts
export async function searchParts(query: string): Promise<PartInfo[]> {
  return fetchJson(`${BASE}/parts/search?q=${encodeURIComponent(query)}`);
}

export async function getPartByKey(itemKey: string): Promise<PartInfo> {
  return fetchJson(`${BASE}/parts/${encodeURIComponent(itemKey)}`);
}

// Arabic
export async function getArabicDescription(itemKey: string): Promise<{ itemKey: string; description: string | null }> {
  return fetchJson(`${BASE}/arabic/${encodeURIComponent(itemKey)}`);
}

export async function saveArabicDescription(itemKey: string, description: string, userName?: string): Promise<void> {
  await fetchJson(`${BASE}/arabic/${encodeURIComponent(itemKey)}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ description, userName: userName ?? 'WebUser' }),
  });
}

export async function deleteArabicDescription(itemKey: string): Promise<void> {
  await fetchJson(`${BASE}/arabic/${encodeURIComponent(itemKey)}`, { method: 'DELETE' });
}

// Stock
export async function getStockInfo(stockId: number): Promise<StockInfo> {
  return fetchJson(`${BASE}/stock/${stockId}`);
}

export async function getStockMoves(stockId: number): Promise<StockMoveItem[]> {
  return fetchJson(`${BASE}/stock/${stockId}/moves`);
}

// Printers
export async function getPrinters(): Promise<PrinterInfo[]> {
  return fetchJson(`${BASE}/printers`);
}

export async function getPrinterStatus(name: string): Promise<PrinterInfo> {
  return fetchJson(`${BASE}/printers/${encodeURIComponent(name)}/status`);
}

// Print
export async function printLabel(request: {
  itemKey: string;
  language: string;
  quantity: number;
  printerName: string;
}): Promise<{ message: string }> {
  return fetchJson(`${BASE}/print/label`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });
}

// Preview
export function getPreviewUrl(itemKey: string, language: string): string {
  return `${BASE}/preview?itemKey=${encodeURIComponent(itemKey)}&language=${encodeURIComponent(language)}`;
}
