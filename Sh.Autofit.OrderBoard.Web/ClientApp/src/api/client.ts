import type { BoardResponse, DeliveryMethodFull, CustomerRule, AccountSearchResult } from '../types';

const BASE = '/api';

async function fetchJson<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
  });
  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText);
    throw new Error(text || res.statusText);
  }
  if (res.status === 204 || res.headers.get('content-length') === '0') return {} as T;
  return res.json();
}

// ---- Board ----

export async function getBoard(includeArchived = false): Promise<BoardResponse> {
  return fetchJson(`${BASE}/board?includeArchived=${includeArchived}`);
}

// ---- Orders ----

export async function createManualOrder(body: {
  accountKey: string;
  accountName?: string;
  city?: string;
  address?: string;
  displayTime?: string;
  note?: string;
}): Promise<{ appOrderId: number }> {
  return fetchJson(`${BASE}/orders/manual`, {
    method: 'POST',
    body: JSON.stringify(body),
  });
}

export async function moveOrder(id: number, toStage: string): Promise<void> {
  await fetchJson(`${BASE}/orders/${id}/move`, {
    method: 'POST',
    body: JSON.stringify({ toStage }),
  });
}

export async function hideOrder(id: number, reason?: string): Promise<void> {
  await fetchJson(`${BASE}/orders/${id}/hide`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
  });
}

export async function unhideOrder(id: number): Promise<void> {
  await fetchJson(`${BASE}/orders/${id}/unhide`, { method: 'POST' });
}

export async function pinOrder(id: number): Promise<void> {
  await fetchJson(`${BASE}/orders/${id}/pin`, { method: 'POST' });
}

export async function unpinOrder(id: number): Promise<void> {
  await fetchJson(`${BASE}/orders/${id}/unpin`, { method: 'POST' });
}

export async function bulkHideByStage(stage: string): Promise<{ count: number }> {
  return fetchJson(`${BASE}/orders/bulk-hide`, {
    method: 'POST',
    body: JSON.stringify({ stage }),
  });
}

export async function assignDelivery(
  id: number,
  deliveryMethodId: number | null,
  deliveryRunId: number | null
): Promise<void> {
  await fetchJson(`${BASE}/orders/${id}/assign-delivery`, {
    method: 'POST',
    body: JSON.stringify({ deliveryMethodId, deliveryRunId }),
  });
}

// ---- Delivery Methods ----

export async function createDeliveryMethod(body: {
  name: string;
  isAdHoc: boolean;
  windowStartTime?: string | null;
  windowEndTime?: string | null;
  rulesJson?: string;
  autoHideAfterMinutes?: number;
}): Promise<{ deliveryMethodId: number }> {
  return fetchJson(`${BASE}/delivery-methods`, {
    method: 'POST',
    body: JSON.stringify(body),
  });
}

export async function closeDeliveryMethod(id: number): Promise<void> {
  await fetchJson(`${BASE}/delivery-methods/${id}/close`, { method: 'POST' });
}

export async function getAllDeliveryMethods(includeInactive = true): Promise<DeliveryMethodFull[]> {
  return fetchJson(`${BASE}/delivery-methods/all?includeInactive=${includeInactive}`);
}

export async function updateDeliveryMethod(id: number, body: {
  name: string;
  isAdHoc: boolean;
  windowStartTime?: string | null;
  windowEndTime?: string | null;
  autoHideAfterMinutes?: number | null;
}): Promise<void> {
  await fetchJson(`${BASE}/delivery-methods/${id}`, {
    method: 'PUT',
    body: JSON.stringify(body),
  });
}

export async function reactivateDeliveryMethod(id: number): Promise<void> {
  await fetchJson(`${BASE}/delivery-methods/${id}/reactivate`, { method: 'POST' });
}

// ---- Delivery Runs ----

export async function openDeliveryRun(body: {
  deliveryMethodId: number;
  windowStart: string;
  windowEnd: string;
}): Promise<{ deliveryRunId: number }> {
  return fetchJson(`${BASE}/delivery-runs/open`, {
    method: 'POST',
    body: JSON.stringify(body),
  });
}

export async function closeDeliveryRun(id: number): Promise<void> {
  await fetchJson(`${BASE}/delivery-runs/${id}/close`, { method: 'POST' });
}

// ---- Customer Rules ----

export async function getAllCustomerRules(): Promise<CustomerRule[]> {
  return fetchJson(`${BASE}/customer-rules`);
}

export async function getRulesByMethod(methodId: number): Promise<CustomerRule[]> {
  return fetchJson(`${BASE}/customer-rules/by-method/${methodId}`);
}

export async function createCustomerRule(body: {
  accountKey: string;
  deliveryMethodId: number;
  windowStart?: string;
  windowEnd?: string;
  daysOfWeek?: string;
}): Promise<{ id: number }> {
  return fetchJson(`${BASE}/customer-rules`, {
    method: 'POST',
    body: JSON.stringify(body),
  });
}

export async function updateCustomerRule(id: number, body: {
  deliveryMethodId: number;
  windowStart?: string;
  windowEnd?: string;
  daysOfWeek?: string;
  isActive: boolean;
}): Promise<void> {
  await fetchJson(`${BASE}/customer-rules/${id}`, {
    method: 'PUT',
    body: JSON.stringify(body),
  });
}

export async function deactivateCustomerRule(id: number): Promise<void> {
  await fetchJson(`${BASE}/customer-rules/${id}`, { method: 'DELETE' });
}

// ---- Account Search ----

export async function searchAccounts(q: string): Promise<AccountSearchResult[]> {
  return fetchJson(`${BASE}/customer-rules/accounts/search?q=${encodeURIComponent(q)}`);
}
