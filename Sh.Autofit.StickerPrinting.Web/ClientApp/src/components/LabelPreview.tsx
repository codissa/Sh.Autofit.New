import { getPreviewUrl } from '../api/client';
import type { Language } from '../types';

interface Props {
  itemKey: string;
  language: Language;
  refreshKey?: number;
}

export default function LabelPreview({ itemKey, language, refreshKey }: Props) {
  if (!itemKey) return null;

  const url = getPreviewUrl(itemKey, language) + (refreshKey ? `&_t=${refreshKey}` : '');

  return (
    <div className="border border-gray-200 rounded-lg p-2 bg-white inline-block">
      <img
        src={url}
        alt={`Label preview for ${itemKey}`}
        className="max-w-full h-auto"
        style={{ imageRendering: 'pixelated' }}
      />
    </div>
  );
}
