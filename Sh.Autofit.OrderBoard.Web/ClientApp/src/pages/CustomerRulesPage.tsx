import { useState, useEffect } from 'react';
import type { CustomerRule, DeliveryMethodFull, AccountSearchResult } from '../types';
import {
  getAllCustomerRules,
  getAllDeliveryMethods,
  createCustomerRule,
  updateCustomerRule,
  deactivateCustomerRule,
  searchAccounts,
} from '../api/client';

const DAYS = [
  { value: '0', label: 'א\'' },
  { value: '1', label: 'ב\'' },
  { value: '2', label: 'ג\'' },
  { value: '3', label: 'ד\'' },
  { value: '4', label: 'ה\'' },
  { value: '5', label: 'ו\'' },
  { value: '6', label: 'ש\'' },
];

interface FormState {
  accountKey: string;
  accountName: string;
  deliveryMethodId: number | null;
  windowStart: string;
  windowEnd: string;
  selectedDays: string[];
}

const emptyForm: FormState = {
  accountKey: '',
  accountName: '',
  deliveryMethodId: null,
  windowStart: '',
  windowEnd: '',
  selectedDays: [],
};

export default function CustomerRulesPage() {
  const [rules, setRules] = useState<CustomerRule[]>([]);
  const [methods, setMethods] = useState<DeliveryMethodFull[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showModal, setShowModal] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [submitting, setSubmitting] = useState(false);

  // Account search
  const [accountQuery, setAccountQuery] = useState('');
  const [accountResults, setAccountResults] = useState<AccountSearchResult[]>([]);
  const [showAccountDropdown, setShowAccountDropdown] = useState(false);

  // Method name lookup
  const methodNameMap = new Map(methods.map((m) => [m.deliveryMethodId, m.name]));

  const load = async () => {
    try {
      const [r, m] = await Promise.all([getAllCustomerRules(), getAllDeliveryMethods(false)]);
      setRules(r);
      setMethods(m);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  // Debounced account search
  useEffect(() => {
    if (accountQuery.length < 2) {
      setAccountResults([]);
      setShowAccountDropdown(false);
      return;
    }
    const timer = setTimeout(async () => {
      try {
        const results = await searchAccounts(accountQuery);
        setAccountResults(results);
        setShowAccountDropdown(results.length > 0);
      } catch {
        setAccountResults([]);
      }
    }, 300);
    return () => clearTimeout(timer);
  }, [accountQuery]);

  const openCreate = () => {
    setEditingId(null);
    setForm(emptyForm);
    setAccountQuery('');
    setShowModal(true);
  };

  const openEdit = (r: CustomerRule) => {
    setEditingId(r.id);
    setForm({
      accountKey: r.accountKey,
      accountName: '',
      deliveryMethodId: r.deliveryMethodId,
      windowStart: r.windowStart ?? '',
      windowEnd: r.windowEnd ?? '',
      selectedDays: r.daysOfWeek ? r.daysOfWeek.split(',') : [],
    });
    setAccountQuery(r.accountKey);
    setShowModal(true);
  };

  const selectAccount = (a: AccountSearchResult) => {
    setForm({ ...form, accountKey: a.accountKey, accountName: a.fullName ?? '' });
    setAccountQuery(`${a.accountKey} - ${a.fullName ?? ''}`);
    setShowAccountDropdown(false);
  };

  const toggleDay = (day: string) => {
    setForm((prev) => ({
      ...prev,
      selectedDays: prev.selectedDays.includes(day)
        ? prev.selectedDays.filter((d) => d !== day)
        : [...prev.selectedDays, day],
    }));
  };

  const handleSubmit = async () => {
    if (!form.accountKey || !form.deliveryMethodId) return;
    setSubmitting(true);
    try {
      const body = {
        accountKey: form.accountKey,
        deliveryMethodId: form.deliveryMethodId,
        windowStart: form.windowStart || undefined,
        windowEnd: form.windowEnd || undefined,
        daysOfWeek: form.selectedDays.length > 0 ? form.selectedDays.join(',') : undefined,
      };

      if (editingId) {
        await updateCustomerRule(editingId, { ...body, isActive: true });
      } else {
        await createCustomerRule(body);
      }
      setShowModal(false);
      await load();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setSubmitting(false);
    }
  };

  const handleDeactivate = async (id: number) => {
    if (!confirm('השבת כלל זה?')) return;
    try {
      await deactivateCustomerRule(id);
      await load();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const formatDays = (daysStr: string | null) => {
    if (!daysStr) return '-';
    return daysStr
      .split(',')
      .map((d) => DAYS.find((day) => day.value === d)?.label ?? d)
      .join(' ');
  };

  if (loading) {
    return <div className="flex items-center justify-center py-20 text-gray-500 text-lg">טוען...</div>;
  }

  return (
    <div className="p-6 max-w-5xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-xl font-bold text-gray-800">כללי משלוח ללקוחות</h2>
        <button
          onClick={openCreate}
          className="px-4 py-2 bg-blue-600 text-white text-sm rounded-lg hover:bg-blue-700 transition-colors"
        >
          + כלל חדש
        </button>
      </div>

      {error && (
        <div className="mb-4 p-3 bg-red-50 text-red-600 rounded-lg text-sm">{error}</div>
      )}

      <div className="bg-white rounded-xl shadow overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 text-gray-600">
            <tr>
              <th className="text-right px-4 py-3 font-medium">מפתח לקוח</th>
              <th className="text-right px-4 py-3 font-medium">שיטת משלוח</th>
              <th className="text-right px-4 py-3 font-medium">חלון זמן</th>
              <th className="text-right px-4 py-3 font-medium">ימים</th>
              <th className="text-right px-4 py-3 font-medium">סטטוס</th>
              <th className="text-right px-4 py-3 font-medium">פעולות</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {rules.map((r) => (
              <tr key={r.id} className={!r.isActive ? 'bg-gray-50 opacity-60' : ''}>
                <td className="px-4 py-3 font-medium text-gray-900">{r.accountKey}</td>
                <td className="px-4 py-3 text-gray-700">
                  {methodNameMap.get(r.deliveryMethodId) ?? `#${r.deliveryMethodId}`}
                </td>
                <td className="px-4 py-3 text-gray-600">
                  {r.windowStart && r.windowEnd ? `${r.windowStart} - ${r.windowEnd}` : '-'}
                </td>
                <td className="px-4 py-3 text-gray-600">{formatDays(r.daysOfWeek)}</td>
                <td className="px-4 py-3">
                  <span className={`px-2 py-0.5 rounded text-xs ${
                    r.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-200 text-gray-500'
                  }`}>
                    {r.isActive ? 'פעיל' : 'מושבת'}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <div className="flex gap-2">
                    {r.isActive && (
                      <>
                        <button
                          onClick={() => openEdit(r)}
                          className="text-blue-600 hover:text-blue-800 text-xs font-medium"
                        >
                          עריכה
                        </button>
                        <button
                          onClick={() => handleDeactivate(r.id)}
                          className="text-red-600 hover:text-red-800 text-xs font-medium"
                        >
                          השבתה
                        </button>
                      </>
                    )}
                  </div>
                </td>
              </tr>
            ))}
            {rules.length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-8 text-center text-gray-400">
                  אין כללי משלוח
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Create/Edit Modal */}
      {showModal && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50">
          <div className="bg-white rounded-xl shadow-2xl p-6 w-full max-w-md mx-4">
            <h3 className="text-lg font-bold mb-4 text-gray-800">
              {editingId ? 'עריכת כלל' : 'כלל חדש'}
            </h3>

            <div className="space-y-4">
              {/* Account search */}
              <div className="relative">
                <label className="block text-sm font-medium text-gray-700 mb-1">לקוח</label>
                <input
                  type="text"
                  value={accountQuery}
                  onChange={(e) => {
                    setAccountQuery(e.target.value);
                    if (!editingId) {
                      setForm({ ...form, accountKey: '', accountName: '' });
                    }
                  }}
                  disabled={!!editingId}
                  placeholder="חפש לפי מפתח או שם..."
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:bg-gray-100"
                />
                {showAccountDropdown && !editingId && (
                  <div className="absolute z-10 w-full mt-1 bg-white border border-gray-200 rounded-lg shadow-lg max-h-48 overflow-y-auto">
                    {accountResults.map((a) => (
                      <button
                        key={a.accountKey}
                        onClick={() => selectAccount(a)}
                        className="w-full text-right px-3 py-2 hover:bg-blue-50 text-sm border-b border-gray-100 last:border-0"
                      >
                        <span className="font-medium">{a.accountKey}</span>
                        {a.fullName && <span className="text-gray-500 mr-2">{a.fullName}</span>}
                        {a.city && <span className="text-gray-400 mr-2">({a.city})</span>}
                      </button>
                    ))}
                  </div>
                )}
                {form.accountKey && (
                  <div className="mt-1 text-xs text-green-600">
                    נבחר: {form.accountKey} {form.accountName && `- ${form.accountName}`}
                  </div>
                )}
              </div>

              {/* Delivery method */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">שיטת משלוח</label>
                <select
                  value={form.deliveryMethodId ?? ''}
                  onChange={(e) => setForm({ ...form, deliveryMethodId: e.target.value ? Number(e.target.value) : null })}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                >
                  <option value="">בחר שיטת משלוח...</option>
                  {methods.map((m) => (
                    <option key={m.deliveryMethodId} value={m.deliveryMethodId}>
                      {m.name} {m.isAdHoc ? '(חד-פעמי)' : ''}
                    </option>
                  ))}
                </select>
              </div>

              {/* Time window */}
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">משעה</label>
                  <input
                    type="time"
                    value={form.windowStart}
                    onChange={(e) => setForm({ ...form, windowStart: e.target.value })}
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">עד שעה</label>
                  <input
                    type="time"
                    value={form.windowEnd}
                    onChange={(e) => setForm({ ...form, windowEnd: e.target.value })}
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                  />
                </div>
              </div>

              {/* Days of week */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">ימים</label>
                <div className="flex gap-2 flex-wrap">
                  {DAYS.map((day) => (
                    <button
                      key={day.value}
                      type="button"
                      onClick={() => toggleDay(day.value)}
                      className={`px-3 py-1.5 rounded text-sm font-medium transition-colors ${
                        form.selectedDays.includes(day.value)
                          ? 'bg-blue-600 text-white'
                          : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
                      }`}
                    >
                      {day.label}
                    </button>
                  ))}
                </div>
              </div>
            </div>

            <div className="flex gap-3 mt-6 justify-end">
              <button
                onClick={() => { setShowModal(false); setShowAccountDropdown(false); }}
                className="px-4 py-2 text-sm text-gray-600 hover:text-gray-800 border border-gray-300 rounded-lg"
              >
                ביטול
              </button>
              <button
                onClick={handleSubmit}
                disabled={submitting || !form.accountKey || !form.deliveryMethodId}
                className="px-4 py-2 text-sm bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:bg-gray-300 transition-colors"
              >
                {submitting ? 'שומר...' : editingId ? 'עדכון' : 'יצירה'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
