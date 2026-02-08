/**
 * Component rendering utilities for client-side HTML generation.
 * These functions mirror the Astro components but generate HTML strings
 * for dynamic content that needs to be rendered client-side.
 */

export interface CardProps {
  value: string | number;
  label: string;
  valueColor?: string;
  class?: string;
}

export interface BadgeProps {
  type: 'error' | 'warning' | 'info' | 'success' | 'added' | 'removed';
  text: string;
  class?: string;
}

export interface MetricBarProps {
  value: number;
  max?: number;
  goodThreshold?: number;
  mediumThreshold?: number;
  class?: string;
}

export interface DeltaIndicatorProps {
  value: number;
  inverseGood?: boolean;
  class?: string;
}

/**
 * Renders a Card component to HTML string.
 */
export function renderCard(props: CardProps): string {
  const { value, label, valueColor, class: className } = props;
  const colorStyle = valueColor ? ` style="color: ${valueColor}"` : '';
  const classAttr = className ? ` ${className}` : '';
  
  return `<div class="card${classAttr}">
  <div class="card-value"${colorStyle}>${value}</div>
  <div class="card-label">${label}</div>
</div>`;
}

/**
 * Renders multiple cards wrapped in a grid container.
 */
export function renderCardsGrid(cards: CardProps[]): string {
  return `<div class="cards">
  ${cards.map(card => renderCard(card)).join('\n  ')}
</div>`;
}

/**
 * Renders a Badge component to HTML string.
 */
export function renderBadge(props: BadgeProps): string {
  const { type, text, class: className } = props;
  const classAttr = className ? ` ${className}` : '';
  
  return `<span class="badge badge-${type}${classAttr}">${text}</span>`;
}

/**
 * Renders a MetricBar component to HTML string.
 */
export function renderMetricBar(props: MetricBarProps): string {
  const { 
    value, 
    max = 100, 
    goodThreshold = 60, 
    mediumThreshold = 40,
    class: className 
  } = props;
  
  const percentage = Math.min(Math.max((value / max) * 100, 0), 100);
  const quality = value >= goodThreshold ? 'mi-good' : value >= mediumThreshold ? 'mi-medium' : 'mi-poor';
  const classAttr = className ? ` ${className}` : '';
  
  return `<div class="metric-bar${classAttr}">
  <div class="metric-bar-fill ${quality}" style="width: ${percentage}%"></div>
</div>`;
}

/**
 * Renders a DeltaIndicator component to HTML string.
 */
export function renderDeltaIndicator(props: DeltaIndicatorProps): string {
  const { value, inverseGood = false, class: className } = props;
  
  const isDown = value < 0;
  const isFlat = value === 0;
  
  let deltaClass = 'delta-flat';
  if (!isFlat) {
    deltaClass = (isDown && !inverseGood) || (!isDown && inverseGood) 
      ? 'delta-down' 
      : 'delta-up';
  }
  
  const sign = value > 0 ? '+' : '';
  const displayValue = isFlat ? '0' : `${sign}${value}`;
  const classAttr = className ? ` ${className}` : '';
  
  return `<span class="delta ${deltaClass}${classAttr}">${displayValue}</span>`;
}

/**
 * Renders a Section wrapper to HTML string.
 */
export function renderSection(heading: string | undefined, content: string): string {
  return `<div class="section">
  ${heading ? `<h2>${heading}</h2>` : ''}
  ${content}
</div>`;
}

/**
 * Escapes HTML special characters to prevent XSS.
 */
export function escapeHtml(text: string): string {
  const map: Record<string, string> = {
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#039;'
  };
  return text.replace(/[&<>"']/g, m => map[m] || m);
}

/**
 * Table rendering utilities
 */
export interface TableColumn {
  header: string;
  key?: string;
  render?: (row: any) => string;
}

export interface TableProps {
  columns: TableColumn[];
  rows: any[];
  class?: string;
}

/**
 * Renders a Table component to HTML string.
 */
export function renderTable(props: TableProps): string {
  const { columns, rows, class: className } = props;
  const classAttr = className ? ` class="${className}"` : '';
  
  const headers = columns.map(col => `<th>${col.header}</th>`).join('');
  
  const rowsHtml = rows.map(row => {
    const cells = columns.map(col => {
      const content = col.render 
        ? col.render(row)
        : (col.key ? row[col.key] : '');
      return `<td>${content}</td>`;
    }).join('');
    return `<tr>${cells}</tr>`;
  }).join('\n        ');
  
  return `<table${classAttr}>
      <thead><tr>${headers}</tr></thead>
      <tbody>${rowsHtml}
      </tbody>
    </table>`;
}

/**
 * Tree component rendering utilities
 */
export interface TreeMetric {
  label: string;
  value: string | number;
}

export interface TreeNodeProps {
  id: string;
  level: 'project' | 'namespace' | 'type' | 'method';
  icon: string;
  label: string;
  metrics: TreeMetric[];
  hasChildren: boolean;
  children?: string;
}

/**
 * Renders tree metrics display.
 */
export function renderTreeMetrics(metrics: TreeMetric[]): string {
  return `<div class="tree-metrics">
    ${metrics.map(m => `<span class="tree-metric"><span class="tree-metric-label">${m.label}:</span> <span class="tree-metric-value">${m.value}</span></span>`).join('\n    ')}
  </div>`;
}

/**
 * Renders a single tree node.
 */
export function renderTreeNode(props: TreeNodeProps): string {
  const { id, level, icon, label, metrics, hasChildren, children = '' } = props;
  const toggleClass = hasChildren ? 'expandable collapsed' : '';
  const nodeIdAttr = hasChildren ? ` data-node-id="${id}"` : '';
  
  return `<li class="tree-node tree-level-${level}">
      <div class="tree-item" data-level="${level}">
        <span class="tree-toggle ${toggleClass}"${nodeIdAttr}></span>
        <div class="tree-label">
          <span class="tree-icon">${icon}</span>
          <span class="tree-label-text">${label}</span>
          ${renderTreeMetrics(metrics)}
        </div>
      </div>
      ${hasChildren && children ? `<ul class="tree-children" data-parent="${id}">
        ${children}
      </ul>` : ''}
    </li>`;
}
