import { useState, useEffect } from 'react';
import type { DeliveryMethodFull } from '../types';
import {
  getAllDeliveryMethods,
  createDeliveryMethod,
  updateDeliveryMethod,
  closeDeliveryMethod,
  reactivateDeliveryMethod,
} from '../api/client';

interface FormState {
  name: string;
  isAdHoc: boolean;
  windowStartTime: string;
  windowEndTime: string;
}

const emptyForm: FormState = { name: '', isAdHoc: false, windowStartTime: '', windowEndTime: '' };

export default function DeliveryMethodsPage() {
  const [methods, setMethods] = useState<DeliveryMethodFull[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showModal, setShowModal] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [submitting, setSubmitting] = useState(false);

  const load = async () => {
    try {
      const data = await getAllDeliveryMethods(true);
      setMethods(data);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const openCreate = () => {
    setEditingId(null);
    setForm(emptyForm);
    setShowModal(true);
  };

  const openEdit = (m: DeliveryMethodFull) => {
    setEditingId(m.deliveryMethodId);
    setForm({
      name: m.name,
      isAdHoc: m.isAdHoc,
      windowStartTime: m.windowStartTime ?? '',
      windowEndTime: m.windowEndTime ?? '',
    });
    setShowModal(true);
  };

  const handleSubmit = async () => {
    if (!form.name.trim()) return;
    setSubmitting(true);
    try {
      const body = {
        name: form.name,
        isAdHoc: form.isAdHoc,
        windowStartTime: form.isAdHoc ? null : (form.windowStartTime || null),
        windowEndTime: form.isAdHoc ? null : (form.windowEndTime || null),
      };

      if (editingId) {
        await updateDeliveryMethod(editingId, body);
      } else {
        await createDeliveryMethod(body);
      }
      setShowModal(false);
      await load();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setSubmitting(false);
    }
  };

  const handleClose = async (id: number) => {
    if (!confirm('סגור שיטת משלוח זו? כל ההזמנות המשויכות יוסתרו.')) return;
    try {
      await closeDeliveryMethod(id);
      await load();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleReactivate = async (id: number) => {
    try {
      await reactivateDeliveryMethod(id);
      await load();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  if (loading) {
    return <div className="flex items-center justify-center py-20 text-gray-500 text-lg">טוען...</div>;
  }

  return (
    <div className="p-6 max-w-4xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <h2 className="text-xl font-bold text-gray-800">שיטות משלוח</h2>
        <button
          onClick={openCreate}
          className="px-4 py-2 bg-purple-600 text-white text-sm rounded-lg hover:bg-purple-700 transition-colors"
        >
          + שיטת משלוח חדשה
        </button>
      </div>

      {error && (
        <div className="mb-4 p-3 bg-red-50 text-red-600 rounded-lg text-sm">{error}</div>
      )}

      <div className="bg-white rounded-xl shadow overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 text-gray-600">
            <tr>
              <th className="text-right px-4 py-3 font-medium">שם</th>
              <th className="text-right px-4 py-3 font-medium">סוג</th>
              <th className="text-right px-4 py-3 font-medium">חלון זמן</th>
              <th className="text-right px-4 py-3 font-medium">סטטוס</th>
              <th className="text-right px-4 py-3 font-medium">פעולות</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {methods.map((m) => (
              <tr key={m.deliveryMethodId} className={!m.isActive ? 'bg-gray-50 opacity-60' : ''}>
                <td className="px-4 py-3 font-medium text-gray-900">{m.name}</td>
                <td className="px-4 py-3">
                  <span className={`px-2 py-0.5 rounded text-xs ${
                    m.isAdHoc ? 'bg-orange-100 text-orange-700' : 'bg-blue-100 text-blue-700'
                  }`}>
                    {m.isAdHoc ? 'חד-פעמי' : 'מתוזמן'}
                  </span>
                </td>
                <td className="px-4 py-3 text-gray-600">
                  {m.windowStartTime && m.windowEndTime
                    ? `${m.windowStartTime} - ${m.windowEndTime}`
                    : '-'}
                </td>
                <td className="px-4 py-3">
                  <span className={`px-2 py-0.5 rounded text-xs ${
                    m.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-200 text-gray-500'
                  }`}>
                    {m.isActive ? 'פעיל' : 'סגור'}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <div className="flex gap-2">
                    {m.isActive && (
                      <>
                        <button
                          onClick={() => openEdit(m)}
                          className="text-blue-600 hover:text-blue-800 text-xs font-medium"
                        >
                          עריכה
                        </button>
                        <button
                          onClick={() => handleClose(m.deliveryMethodId)}
                          className="text-red-600 hover:text-red-800 text-xs font-medium"
                        >
                          סגירה
                        </button>
                      </>
                    )}
                    {!m.isActive && (
                      <button
                        onClick={() => handleReactivate(m.deliveryMethodId)}
                        className="text-green-600 hover:text-green-800 text-xs font-medium"
                      >
                        הפעלה מחדש
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
            {methods.length === 0 && (
              <tr>
                <td colSpan={5} className="px-4 py-8 text-center text-gray-400">
                  אין שיטות משלוח
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
              {editingId ? 'עריכת שיטת משלוח' : 'שיטת משלוח חדשה'}
            </h3>

            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">שם</label>
                <input
                  type="text"
                  value={form.name}
                  onChange={(e) => setForm({ ...form, name: e.target.value })}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-purple-500"
                  placeholder="שם שיטת המשלוח"
                />
              </div>

              <div className="flex items-center gap-2">
                <input
                  type="checkbox"
                  id="isAdHoc"
                  checked={form.isAdHoc}
                  onChange={(e) => setForm({ ...form, isAdHoc: e.target.checked })}
                  className="rounded"
                />
                <label htmlFor="isAdHoc" className="text-sm text-gray-700">
                  חד-פעמי (נשאר עד סגירה ידנית)
                </label>
              </div>

              {!form.isAdHoc && (
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">שעת התחלה</label>
                    <input
                      type="time"
                      value={form.windowStartTime}
                      onChange={(e) => setForm({ ...form, windowStartTime: e.target.value })}
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-purple-500"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">שעת סיום</label>
                    <input
                      type="time"
                      value={form.windowEndTime}
                      onChange={(e) => setForm({ ...form, windowEndTime: e.target.value })}
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-purple-500"
                    />
                  </div>
                </div>
              )}
            </div>

            <div className="flex gap-3 mt-6 justify-end">
              <button
                onClick={() => setShowModal(false)}
                className="px-4 py-2 text-sm text-gray-600 hover:text-gray-800 border border-gray-300 rounded-lg"
              >
                ביטול
              </button>
              <button
                onClick={handleSubmit}
                disabled={submitting || !form.name.trim()}
                className="px-4 py-2 text-sm bg-purple-600 text-white rounded-lg hover:bg-purple-700 disabled:bg-gray-300 transition-colors"
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
