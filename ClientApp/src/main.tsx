import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { Root } from './Root';
import './styles/index.css';
import './styles/premium-dashboard.css';

const container = document.getElementById('root');

if (!container) {
  throw new Error('The #root element of index.html is missing.');
}

createRoot(container).render(
  <StrictMode>
    <Root pathname={window.location.pathname} />
  </StrictMode>,
);
