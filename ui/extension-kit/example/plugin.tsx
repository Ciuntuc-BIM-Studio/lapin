/**
 * The extension kit's example plug-in (the tests build it): a ribbon tab with one button, a view, and its phrases.
 * Its class names start with its id, as the catalogue's validator asks.
 */
import { useState } from 'react';
import { Sparkles } from 'lucide-react';
import { notify, t, version } from '@lapin/sdk';
import './plugin.css';

function HelloView() {
  const [n, setN] = useState(0);
  return (
    <div className="hello-ext-view">
      <button type="button" onClick={() => setN(n + 1)}>Say hello</button>
      <span>{t(`${n} hellos`)}</span>
    </div>
  );
}

export default {
  id: 'hello-ext',
  name: 'Hello',
  summary: `An example extension (SDK ${version}).`,
  tabs: [{ id: 'hello', label: 'Hello', order: 900, groups: [{ label: 'Greetings', tools: [{ id: 'hello-ext.say', label: 'Say hello', icon: Sparkles, run: () => notify(t('Hello from an extension')) }] }] }],
  views: { 'hello-ext.view': { label: 'Hello', component: HelloView } },
};
