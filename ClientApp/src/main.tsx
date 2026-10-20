import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import './styles/index.css';

const container = document.getElementById('root');

if (!container) {
  throw new Error('The #root element of index.html is missing.');
}

createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
