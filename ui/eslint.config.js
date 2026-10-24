// ESLint for the UI: typescript-eslint's recommended rules (not type-checked,
// to stay fast), the rules of hooks and Vite's fast-refresh constraints.
import js from '@eslint/js';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  { ignores: ['dist', 'node_modules'] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  reactRefresh.configs.vite,
  {
    files: ['**/*.{ts,tsx}'],
    languageOptions: {
      ecmaVersion: 2022,
      globals: { ...globals.browser, ...globals.node },
    },
    plugins: { 'react-hooks': reactHooks },
    rules: {
      'react-hooks/rules-of-hooks': 'error',
      'react-hooks/exhaustive-deps': 'warn',
      // Module index files export the module definition next to its
      // components; that only costs fast-refresh granularity, so a warning.
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
    },
  },
);
