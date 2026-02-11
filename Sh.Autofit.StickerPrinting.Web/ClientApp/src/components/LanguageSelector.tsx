import type { Language } from '../types';

interface Props {
  value: Language;
  onChange: (lang: Language) => void;
}

export default function LanguageSelector({ value, onChange }: Props) {
  return (
    <div className="flex rounded-lg overflow-hidden border border-gray-300">
      <button
        type="button"
        onClick={() => onChange('he')}
        className={`px-4 py-2.5 text-sm font-medium transition-colors ${
          value === 'he'
            ? 'bg-blue-600 text-white'
            : 'bg-white text-gray-700 hover:bg-gray-100'
        }`}
      >
        Hebrew
      </button>
      <button
        type="button"
        onClick={() => onChange('ar')}
        className={`px-4 py-2.5 text-sm font-medium transition-colors ${
          value === 'ar'
            ? 'bg-blue-600 text-white'
            : 'bg-white text-gray-700 hover:bg-gray-100'
        }`}
      >
        Arabic
      </button>
    </div>
  );
}
