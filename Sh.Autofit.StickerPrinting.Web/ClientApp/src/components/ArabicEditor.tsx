import { useState } from 'react';
import { saveArabicDescription, deleteArabicDescription } from '../api/client';

interface Props {
  itemKey: string;
  currentDescription: string;
  onSaved: (newDesc: string) => void;
  onDeleted: () => void;
  onClose: () => void;
}

export default function ArabicEditor({ itemKey, currentDescription, onSaved, onDeleted, onClose }: Props) {
  const [text, setText] = useState(currentDescription);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSave() {
    if (!text.trim()) return;
    setSaving(true);
    setError(null);
    try {
      await saveArabicDescription(itemKey, text.trim());
      onSaved(text.trim());
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Save failed');
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete() {
    setSaving(true);
    setError(null);
    try {
      await deleteArabicDescription(itemKey);
      onDeleted();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Delete failed');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-xl shadow-xl w-full max-w-md">
        <div className="p-4 border-b border-gray-200">
          <h3 className="text-lg font-semibold">Edit Arabic Description</h3>
          <p className="text-sm text-gray-500 mt-1">Item: {itemKey}</p>
        </div>
        <div className="p-4">
          <textarea
            dir="rtl"
            value={text}
            onChange={e => setText(e.target.value)}
            rows={4}
            className="w-full border border-gray-300 rounded-lg px-3 py-2 text-base focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
            placeholder="Enter Arabic description..."
            style={{ fontFamily: 'Arial, sans-serif', fontSize: '16px' }}
          />
          {error && <p className="text-red-500 text-sm mt-2">{error}</p>}
        </div>
        <div className="p-4 border-t border-gray-200 flex gap-2 justify-between">
          <button
            onClick={handleDelete}
            disabled={saving}
            className="px-4 py-2 text-sm text-red-600 hover:bg-red-50 rounded-lg disabled:opacity-50"
          >
            Delete
          </button>
          <div className="flex gap-2">
            <button
              onClick={onClose}
              disabled={saving}
              className="px-4 py-2 text-sm text-gray-600 hover:bg-gray-100 rounded-lg disabled:opacity-50"
            >
              Cancel
            </button>
            <button
              onClick={handleSave}
              disabled={saving || !text.trim()}
              className="px-4 py-2 text-sm bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50"
            >
              {saving ? 'Saving...' : 'Save'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
