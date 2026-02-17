import { useState } from 'react';
import { createDeliveryMethod } from '../../api/client';

interface Props {
  onClose: () => void;
  onCreated: () => void;
}

export default function CreateDeliveryMethodModal({ onClose, onCreated }: Props) {
  const [name, setName] = useState('');
  const [isAdHoc, setIsAdHoc] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setError('שם שיטת משלוח הוא שדה חובה');
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      await createDeliveryMethod({
        name: name.trim(),
        isAdHoc,
      });
      onCreated();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'שגיאה ביצירת שיטת משלוח');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40" onClick={onClose}>
      <div
        className="bg-white rounded-xl shadow-2xl w-full max-w-md mx-4 p-6"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="text-lg font-bold text-gray-800 mb-4">שיטת משלוח חדשה</h2>

        <form onSubmit={handleSubmit} className="space-y-3">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">שם *</label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="לדוגמה: שליח צפון, איסוף עצמי..."
              className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-purple-500"
              autoFocus
            />
          </div>

          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              id="isAdHoc"
              checked={isAdHoc}
              onChange={(e) => setIsAdHoc(e.target.checked)}
              className="rounded border-gray-300"
            />
            <label htmlFor="isAdHoc" className="text-sm text-gray-700">
              חד פעמי (אד-הוק)
            </label>
          </div>

          {error && <div className="text-sm text-red-600">{error}</div>}

          <div className="flex gap-3 pt-2">
            <button
              type="submit"
              disabled={submitting}
              className="flex-1 px-4 py-2 bg-purple-600 text-white text-sm rounded-lg hover:bg-purple-700 disabled:opacity-50"
            >
              {submitting ? 'יוצר...' : 'צור שיטת משלוח'}
            </button>
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-sm text-gray-600 border border-gray-300 rounded-lg hover:bg-gray-50"
            >
              ביטול
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
