import { useState, useEffect, useRef } from 'react';
import { MapPin, Search, Loader2, Navigation } from 'lucide-react';

const GOONG_API_KEY = import.meta.env.VITE_GOONG_API_KEY;
const GOONG_BASE = 'https://rsapi.goong.io';

/**
 * Goong.io Address Autocomplete component
 * - Autocomplete khi gõ địa chỉ
 * - Geocode để lấy lat/lng
 * - Nút "Lấy vị trí hiện tại" bằng GPS trình duyệt
 */
export default function GoongAutocomplete({ value, onChange, onSelect, placeholder }) {
  const [query, setQuery] = useState(value || '');
  const [suggestions, setSuggestions] = useState([]);
  const [loading, setLoading] = useState(false);
  const [showDropdown, setShowDropdown] = useState(false);
  const [gpsLoading, setGpsLoading] = useState(false);
  const debounceRef = useRef(null);
  const wrapperRef = useRef(null);

  // Close dropdown when clicking outside
  useEffect(() => {
    const handleClick = (e) => {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target)) {
        setShowDropdown(false);
      }
    };
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, []);

  // Sync external value
  useEffect(() => {
    if (value !== undefined && value !== query) setQuery(value);
  }, [value]);

  // Autocomplete search with debounce
  const handleInputChange = (e) => {
    const val = e.target.value;
    setQuery(val);
    onChange?.(val);

    if (debounceRef.current) clearTimeout(debounceRef.current);

    if (val.trim().length < 2) {
      setSuggestions([]);
      setShowDropdown(false);
      return;
    }

    debounceRef.current = setTimeout(async () => {
      if (!GOONG_API_KEY || GOONG_API_KEY === 'your_goong_api_key_here') {
        // Fallback: no API key, just use text input
        return;
      }
      setLoading(true);
      try {
        const res = await fetch(
          `${GOONG_BASE}/Place/AutoComplete?input=${encodeURIComponent(val)}&api_key=${GOONG_API_KEY}`
        );
        const data = await res.json();
        if (data.predictions) {
          setSuggestions(data.predictions);
          setShowDropdown(true);
        }
      } catch {
        // silently fail
      } finally {
        setLoading(false);
      }
    }, 400);
  };

  // When user selects a suggestion, geocode it to get lat/lng
  const handleSelectSuggestion = async (suggestion) => {
    const address = suggestion.description;
    setQuery(address);
    setSuggestions([]);
    setShowDropdown(false);
    onChange?.(address);

    // Get lat/lng from place_id via Goong Geocode
    if (suggestion.place_id && GOONG_API_KEY && GOONG_API_KEY !== 'your_goong_api_key_here') {
      try {
        const res = await fetch(
          `${GOONG_BASE}/Place/Detail?place_id=${suggestion.place_id}&api_key=${GOONG_API_KEY}`
        );
        const data = await res.json();
        const location = data.result?.geometry?.location;
        if (location) {
          onSelect?.({ address, lat: location.lat, lng: location.lng });
        } else {
          onSelect?.({ address, lat: null, lng: null });
        }
      } catch {
        onSelect?.({ address, lat: null, lng: null });
      }
    } else {
      onSelect?.({ address, lat: null, lng: null });
    }
  };

  // Get current GPS location
  const handleGetCurrentLocation = () => {
    if (!navigator.geolocation) {
      return;
    }
    setGpsLoading(true);
    navigator.geolocation.getCurrentPosition(
      async (pos) => {
        const { latitude, longitude } = pos.coords;
        // Reverse geocode via Goong
        if (GOONG_API_KEY && GOONG_API_KEY !== 'your_goong_api_key_here') {
          try {
            const res = await fetch(
              `${GOONG_BASE}/Geocode?latlng=${latitude},${longitude}&api_key=${GOONG_API_KEY}`
            );
            const data = await res.json();
            const address = data.results?.[0]?.formatted_address || `${latitude}, ${longitude}`;
            setQuery(address);
            onChange?.(address);
            onSelect?.({ address, lat: latitude, lng: longitude });
          } catch {
            const address = `${latitude.toFixed(6)}, ${longitude.toFixed(6)}`;
            setQuery(address);
            onChange?.(address);
            onSelect?.({ address, lat: latitude, lng: longitude });
          }
        } else {
          const address = `${latitude.toFixed(6)}, ${longitude.toFixed(6)}`;
          setQuery(address);
          onChange?.(address);
          onSelect?.({ address, lat: latitude, lng: longitude });
        }
        setGpsLoading(false);
      },
      () => {
        setGpsLoading(false);
      },
      { enableHighAccuracy: true, timeout: 10000 }
    );
  };

  return (
    <div ref={wrapperRef} className="relative">
      <div className="relative">
        <MapPin size={16} className="absolute left-4 top-1/2 -translate-y-1/2 text-emerald-400 z-10" />
        <input
          value={query}
          onChange={handleInputChange}
          onFocus={() => suggestions.length > 0 && setShowDropdown(true)}
          placeholder={placeholder || 'Nhập địa chỉ thu gom...'}
          className="w-full bg-slate-900 border border-slate-700 rounded-xl pl-11 pr-24 py-3 text-sm text-slate-200 placeholder-slate-600 focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 outline-none transition-all"
        />
        <div className="absolute right-2 top-1/2 -translate-y-1/2 flex items-center gap-1">
          {loading && <Loader2 size={16} className="text-emerald-400 animate-spin" />}
          <button
            type="button"
            onClick={handleGetCurrentLocation}
            disabled={gpsLoading}
            className="p-1.5 rounded-lg bg-emerald-500/10 text-emerald-400 hover:bg-emerald-500/20 transition-colors disabled:opacity-50"
            title="Lấy vị trí hiện tại"
          >
            {gpsLoading ? (
              <Loader2 size={14} className="animate-spin" />
            ) : (
              <Navigation size={14} />
            )}
          </button>
        </div>
      </div>

      {/* Dropdown suggestions */}
      {showDropdown && suggestions.length > 0 && (
        <div className="absolute z-50 w-full mt-1.5 bg-slate-900 border border-slate-700 rounded-xl shadow-2xl overflow-hidden max-h-60 overflow-y-auto">
          {suggestions.map((s, i) => (
            <button
              key={s.place_id || i}
              onClick={() => handleSelectSuggestion(s)}
              className="w-full text-left px-4 py-3 hover:bg-slate-800 transition-colors flex items-start gap-3 border-b border-slate-800/50 last:border-0"
            >
              <MapPin size={14} className="text-slate-500 shrink-0 mt-0.5" />
              <div className="min-w-0">
                <p className="text-sm text-slate-200 truncate">{s.structured_formatting?.main_text || s.description}</p>
                <p className="text-xs text-slate-500 truncate mt-0.5">{s.structured_formatting?.secondary_text || ''}</p>
              </div>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
