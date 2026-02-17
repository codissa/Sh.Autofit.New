import { useState } from 'react';
import ItemSearch from '../components/ItemSearch';
import LanguageSelector from '../components/LanguageSelector';
import LabelPreview from '../components/LabelPreview';
import PrinterSelector from '../components/PrinterSelector';
import ArabicEditor from '../components/ArabicEditor';
import { printLabel } from '../api/client';
import type { PartInfo, Language } from '../types';

export default function PrintOnDemand() {
  const [selectedPart, setSelectedPart] = useState<PartInfo | null>(null);
  const [language, setLanguage] = useState<Language>('he');
  const [quantity, setQuantity] = useState(10);
  const [printer, setPrinter] = useState('');
  const [printing, setPrinting] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [editingArabic, setEditingArabic] = useState(false);
  const [previewRefreshKey, setPreviewRefreshKey] = useState(0);

  function handlePartSelect(part: PartInfo) {
    setSelectedPart(part);
    setMessage(null);
  }

  async function handlePrint() {
    if (!selectedPart || !printer) return;
    setPrinting(true);
    setMessage(null);
    try {
      const res = await printLabel({
        itemKey: selectedPart.itemKey,
        language,
        quantity,
        printerName: printer,
      });
      setMessage(res.message);
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Print failed');
    } finally {
      setPrinting(false);
    }
  }

  function handleArabicSaved(newDesc: string) {
    if (selectedPart) {
      setSelectedPart({ ...selectedPart, arabicDescription: newDesc });
    }
    setEditingArabic(false);
    setPreviewRefreshKey(k => k + 1);
  }

  function handleArabicDeleted() {
    if (selectedPart) {
      setSelectedPart({ ...selectedPart, arabicDescription: null });
    }
    setEditingArabic(false);
    setPreviewRefreshKey(k => k + 1);
  }

  return (
    <div className="space-y-6">
      <h2 className="text-xl font-bold text-gray-800">Print on Demand</h2>

      <div className="grid md:grid-cols-2 gap-6">
        {/* Left: Form */}
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Item Key</label>
            <ItemSearch onSelect={handlePartSelect} />
          </div>

          {selectedPart && (
            <div className="bg-blue-50 border border-blue-200 rounded-lg p-3">
              <p className="font-semibold text-sm">{selectedPart.itemKey}</p>
              <p className="text-sm text-gray-600 mt-1">
                {selectedPart.hebrewDescription || selectedPart.partName}
              </p>
              {selectedPart.localization && (
                <p className="text-xs text-gray-500 mt-1">Location: {selectedPart.localization}</p>
              )}
              {selectedPart.stockQuantity != null && (
                <p className="text-xs text-orange-700 mt-1">Stock: {selectedPart.stockQuantity}</p>
              )}
            </div>
          )}

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Language</label>
            <LanguageSelector value={language} onChange={setLanguage} />
          </div>

          {language === 'ar' && selectedPart && (
            <button
              onClick={() => setEditingArabic(true)}
              className="text-sm text-blue-600 hover:text-blue-800 underline"
            >
              Edit Arabic Description
            </button>
          )}

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Quantity</label>
            <input
              type="number"
              min={1}
              value={quantity || ''}
              onChange={e => setQuantity(parseInt(e.target.value) || 0)}
              onBlur={() => setQuantity(q => Math.max(1, q))}
              className="w-24 border border-gray-300 rounded-lg px-3 py-2.5 text-sm focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Printer</label>
            <PrinterSelector value={printer} onChange={setPrinter} />
          </div>

          <button
            onClick={handlePrint}
            disabled={!selectedPart || !printer || printing}
            className="w-full py-3 bg-green-600 text-white rounded-lg font-medium hover:bg-green-700 disabled:bg-gray-300 disabled:cursor-not-allowed text-sm transition-colors"
          >
            {printing ? 'Printing...' : `Print ${quantity} Labels`}
          </button>

          {message && (
            <div className={`p-3 rounded-lg text-sm ${message.toLowerCase().includes('error') || message.toLowerCase().includes('fail') ? 'bg-red-50 text-red-700' : 'bg-green-50 text-green-700'}`}>
              {message}
            </div>
          )}
        </div>

        {/* Right: Preview */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Label Preview</label>
          {selectedPart ? (
            <LabelPreview
              itemKey={selectedPart.itemKey}
              language={language}
              refreshKey={previewRefreshKey}
            />
          ) : (
            <div className="border border-dashed border-gray-300 rounded-lg p-8 text-center text-sm text-gray-400">
              Select an item to see preview
            </div>
          )}
        </div>
      </div>

      {editingArabic && selectedPart && (
        <ArabicEditor
          itemKey={selectedPart.itemKey}
          currentDescription={selectedPart.arabicDescription ?? ''}
          onSaved={handleArabicSaved}
          onDeleted={handleArabicDeleted}
          onClose={() => setEditingArabic(false)}
        />
      )}
    </div>
  );
}
