/** @type {import('tailwindcss').Config} */
const primeui = require('tailwindcss-primeui');

module.exports = {
  darkMode: ['selector', '[data-theme="dark"]'],
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      colors: {
        // Water-domain palette (FSMS) — mirrors semantic.primary in
        // src/app/core/theme/water-preset.ts. That preset is the source of
        // truth; update both together.
        'water-deep': '#145e77',
        water: '#157591',
        aqua: '#248fac',
        teal: '#45adc7',
        foam: '#f0f9fb',

        // Theme-reactive app surfaces. Prefer `bg-app-surface` /
        // `border-app-border` over arbitrary `bg-[var(--app-surface)]`.
        app: {
          ground: 'var(--app-ground)',
          surface: 'var(--app-surface)',
          hover: 'var(--app-hover)',
          border: 'var(--app-border)',
        },
      },
      boxShadow: {
        'app-sm': 'var(--app-shadow-sm)',
        'app-md': 'var(--app-shadow-md)',
        'app-lg': 'var(--app-shadow-lg)',
      },
      fontFamily: {
        sans: ['Inter', 'Tajawal', 'system-ui', 'sans-serif'],
      },
      keyframes: {
        'fade-in-up': {
          '0%': { opacity: '0', transform: 'translateY(16px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        'slide-in-start': {
          '0%': { opacity: '0', transform: 'translateX(-40px)' },
          '100%': { opacity: '1', transform: 'translateX(0)' },
        },
        'slide-in-end': {
          '0%': { opacity: '0', transform: 'translateX(40px)' },
          '100%': { opacity: '1', transform: 'translateX(0)' },
        },
        float: {
          '0%, 100%': { transform: 'translateY(0)' },
          '50%': { transform: 'translateY(-18px)' },
        },
        wave: {
          '0%': { transform: 'translateX(0)' },
          '100%': { transform: 'translateX(-50%)' },
        },
      },
      animation: {
        'fade-in-up': 'fade-in-up 0.6s ease-out both',
        'slide-in-start': 'slide-in-start 0.7s cubic-bezier(0.16,1,0.3,1) both',
        'slide-in-end': 'slide-in-end 0.7s cubic-bezier(0.16,1,0.3,1) both',
        float: 'float 6s ease-in-out infinite',
        'wave-slow': 'wave 12s linear infinite',
        'wave-fast': 'wave 7s linear infinite',
      },
    },
  },
  plugins: [primeui],
};
