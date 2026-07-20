import { useState, useEffect, useRef } from 'react';
import { supabase } from './supabase';
import {
  Plus, Save, Layout, Type, Image as ImageIcon,
  Square, Circle, Trash2, Cloud,
  User, Sparkles,
  Eye, EyeOff, AlignLeft, AlignCenter, AlignRight,
  Bold, Italic, Grid as GridIcon, Signature, Hash,
  Smartphone, MapPin, Package, Cpu, Terminal, Underline,
  Box, MousePointer2, Send, Key, FileCode, Minus, Hexagon, Triangle
} from 'lucide-react';
import KeyManager from './KeyManager';
import UpdateManager from './UpdateManager';

interface TableCellData {
  Row: number;
  Col: number;
  RowSpan: number;
  ColSpan: number;
  Text: string;
  BackgroundColor: string;
  BorderColor: string;
  BorderL: number;
  BorderT: number;
  BorderR: number;
  BorderB: number;
  BorderStyle: 'Solid' | 'Dashed' | 'Dotted' | 'None';
  TextAlignment: 'Left' | 'Center' | 'Right';
  IsBold?: boolean;
  IsItalic?: boolean;
}

interface DesignerBlock {
  Id: string;
  X: number;
  Y: number;
  Width: number;
  Height: number;
  ColorHex: string;
  FontSize: number;
  FontFamily: string;
  Opacity: number;
  IsBold: boolean;
  IsItalic: boolean;
  IsUnderlined?: boolean;
  CustomText: string;
  IsHalfA4: boolean;
  TextAlignment: 'Left' | 'Center' | 'Right';
  BorderRadius: number;
  TableRows: number;
  TableCols: number;
  TableCellsJson: string; // JSON string of TableCellData[]
  TableColumnWidths?: number[]; // as percentages
  TableRowHeights?: number[];  // as percentages
  VisibilityCondition: string;
  ImagePath?: string;
  TableBackgroundColorHex?: string;
  BorderColorHex?: string;
  ShapeBorderThickness?: number;
  PolygonSides?: number;
  FormattedTextXaml?: string;
  BackgroundColorHex?: string;
}

const DUMMY_DATA: Record<string, string> = {
  name: "ANTIGRAVITY SERVICE CENTER",
  address: "456 Tech Boulevard, Silicon Valley, CA 94043",
  phone: "+1 (555) 0123 4567",
  memo_id: "SRV-2026-8892",
  date: new Date().toLocaleDateString(),
  customer_name: "John Doe",
  customer_phone: "+1 987 654 3210",
  customer_address: "789 Residential Way, Apt 12B",
  brand: "Apple",
  model: "MacBook Pro M3 Max",
  product_name: "iPhone 15 Pro Max",
  serial_number: "SN-9988776655",
  accessories: "Charger, Protective Sleeve",
  issue: "Screen flickering and battery health at 78%",
  diagnostics: "Initial inspection confirmed panel issue. Parts ordered.",
  technician_name: "Alex Rivera",
  cost: "$1,250.00",
  customer_signature: "____________________ (Sign Here)",
  company_signature: "Manager / Authorized Signatory"
};

const TOOLBOX_ITEMS = [
  { id: 'custom_text', name: 'Text Block', icon: <Type size={18} /> },
  { id: 'logo', name: 'Brand Logo', icon: <ImageIcon size={18} /> },
  { id: 'custom_image', name: 'Custom Image', icon: <ImageIcon size={18} style={{ opacity: 0.7 }} /> },
  { id: 'line', name: 'Separator Line', icon: <Minus size={18} /> },
  { id: 'rect', name: 'Rectangle', icon: <Square size={18} /> },
  { id: 'circle', name: 'Circle', icon: <Circle size={18} /> },
  { id: 'triangle', name: 'Triangle', icon: <Triangle size={18} /> },
  { id: 'polygon', name: 'Polygon', icon: <Hexagon size={18} /> },
  { id: 'table', name: 'Data Table', icon: <GridIcon size={18} /> },
];

const PRESET_FIELDS = [
  { id: 'customer_name', name: 'Customer Name', field: '{customer_name}', icon: <User size={16} /> },
  { id: 'customer_phone', name: 'Customer Phone', field: '{customer_phone}', icon: <Smartphone size={16} /> },
  { id: 'customer_address', name: 'Customer Address', field: '{customer_address}', icon: <MapPin size={16} /> },
  { id: 'memo_id', name: 'Memo ID / No.', field: '{memo_id}', icon: <Hash size={16} /> },
  { id: 'brand_model', name: 'Product / Model', field: '{brand} {model}', icon: <Package size={16} /> },
  { id: 'product_name', name: 'Product Name', field: '{product_name}', icon: <Package size={16} /> },
  { id: 'brand', name: 'Brand Name', field: '{brand}', icon: <Package size={16} /> },
  { id: 'model', name: 'Model Number', field: '{model}', icon: <Cpu size={16} /> },
  { id: 'serial_number', name: 'Serial Number', field: '{serial_number}', icon: <Cpu size={16} /> },
  { id: 'accessories', name: 'Accessories', field: '{accessories}', icon: <Package size={16} /> },
  { id: 'issue', name: 'Complaint / Issue', field: '{issue}', icon: <FileCode size={16} /> },
  { id: 'diagnostics', name: 'Diagnostics & Notes', field: '{diagnostics}', icon: <FileCode size={16} /> },
  { id: 'cost', name: 'Estimated Cost', field: '{cost}', icon: <Hash size={16} /> },
  { id: 'technician', name: 'Technician', field: '{technician_name}', icon: <Terminal size={16} /> },
  { id: 'signature', name: 'Customer Signature', field: '{customer_signature}', icon: <Signature size={16} /> },
  { id: 'comp_signature', name: 'Company Signature', field: '{company_signature}', icon: <Signature size={16} /> },
  { id: 'comp_name', name: 'Company Name', field: '{name}', icon: <Box size={16} /> },
  { id: 'comp_address', name: 'Company Address', field: '{address}', icon: <MapPin size={16} /> },
  { id: 'comp_phone', name: 'Company Phone', field: '{phone}', icon: <Smartphone size={16} /> },
];

export const AI_SYSTEM_PROMPT = `You are an elite PDF template architect. Your mission is to generate a professional Service Memo/Invoice template in JSON format.
CRITICAL DIMENSIONS: A4 Page is 794px wide x 1123px high.
You MUST output a raw JSON array of DesignerBlock objects. No markdown, no talk.

INTERFACE SPECS:
- Id: 'rect' | 'custom_text' | 'table' | 'circle'
- X, Y, Width, Height: numbers
- ColorHex: string (e.g. #1A73E8)
- FontSize: number (10-30)
- FontFamily: 'Inter'
- CustomText: string. Use these placeholders: {name}, {address}, {phone}, {memo_id}, {date}, {customer_name}, {customer_phone}, {customer_address}, {brand}, {model}, {serial_number}, {issue}, {technician_name}, {cost}, {customer_signature}.
- TextAlignment: 'Left' | 'Center' | 'Right'
- TableRows, TableCols: numbers
- TableColumnWidths: number[] (widths in pixels, total must match block Width)
- TableRowHeights: number[] (heights in pixels, total must match block Height)
- TableCellsJson: string (A JSON-stringified array of TableCellData)

DESIGN RULES:
1. Use 'rect' for headers or backgrounds.
2. Ensure placeholders are used logically (e.g. Header has {name}, Body has {customer_name}).
3. For theme requests (e.g. "Green and White"), use professional shades (e.g. #064e3b, #10b981).
4. Tables must have clear borders and header row colors.
5. Create a layout that is visually stunning and functionally complete.`;

const FONT_FAMILIES = ['Inter', 'Roboto', 'Serif', 'Monospace', 'Georgia', 'Arial'];

const getPolygonPoints = (sides: number) => {
  const points: string[] = [];
  for (let i = 0; i < sides; i++) {
    const angle = (2 * Math.PI * i) / sides - Math.PI / 2;
    const x = 50 + 50 * Math.cos(angle);
    const y = 50 + 50 * Math.sin(angle);
    points.push(`${x},${y}`);
  }
  return points.join(' ');
};


const loadPdfJs = () => {
  return new Promise<any>((resolve) => {
    if ((window as any).pdfjsLib) {
      resolve((window as any).pdfjsLib);
      return;
    }
    const script = document.createElement('script');
    script.src = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/2.16.105/pdf.min.js';
    script.onload = () => {
      (window as any).pdfjsLib.GlobalWorkerOptions.workerSrc = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/2.16.105/pdf.worker.min.js';
      resolve((window as any).pdfjsLib);
    };
    document.head.appendChild(script);
  });
};

function App() {
  const [blocks, setBlocks] = useState<DesignerBlock[]>([]);
  const [selectedBlockIndex, setSelectedBlockIndex] = useState<number | null>(null);
  const [selectedCellPos, setSelectedCellPos] = useState<{ row: number; col: number } | null>(null);
  const [isHalfA4, setIsHalfA4] = useState(false);
  const [templateName, setTemplateName] = useState('Professional Cloud Template');
  const [authorName, setAuthorName] = useState('Admin');
  const [isSaving, setIsSaving] = useState(false);
  const [zoom, setZoom] = useState(0.4);
  const [showDummyData, setShowDummyData] = useState(false);
  const [aiPrompt, setAiPrompt] = useState('');
  const [isAiGenerating, setIsAiGenerating] = useState(false);
  const [editingBlockIndex, setEditingBlockIndex] = useState<number | null>(null);
  const [editingCellPos, setEditingCellPos] = useState<{ row: number; col: number } | null>(null);
  const [resizing, setResizing] = useState<{ blockIndex: number; type: 'row' | 'col'; index: number; startPos: number; startSize: number; initialTotalSize: number } | null>(null);
  const [blockResizing, setBlockResizing] = useState<{ index: number; startX: number; startY: number; startWidth: number; startHeight: number } | null>(null);
  const [panOffset, setPanOffset] = useState({ x: 0, y: 0 });
  const [isDarkMode, setIsDarkMode] = useState(() => localStorage.getItem('isDarkMode') === 'true');

  const [view, setView] = useState<'dashboard' | 'editor' | 'templates' | 'keys' | 'updates'>('dashboard');
  const [savedTemplates, setSavedTemplates] = useState<any[]>([]);
  const [totalKeys, setTotalKeys] = useState(0);
  const [latestVersion, setLatestVersion] = useState('1.0.0');
  const [latestVersionType, setLatestVersionType] = useState('minor');
  const [currentTemplateId, setCurrentTemplateId] = useState<string | null>(null);
  const [isPublishing, setIsPublishing] = useState<string | null>(null); // Stores template ID being published
  const [isDigitalOceanEnabled, setIsDigitalOceanEnabled] = useState(false);
  const [remainingFreeSpaceMb, setRemainingFreeSpaceMb] = useState(500);

  useEffect(() => {
    if (isDarkMode) {
      document.body.classList.add('dark-mode');
    } else {
      document.body.classList.remove('dark-mode');
    }
  }, [isDarkMode]);

  useEffect(() => {
    localStorage.setItem('isDarkMode', isDarkMode.toString());
  }, [isDarkMode]);

  const [history, setHistory] = useState<DesignerBlock[][]>([]);
  const [historyIndex, setHistoryIndex] = useState(-1);

  const [dragging, setDragging] = useState<{ index: number; startX: number; startY: number; blockX: number; blockY: number } | null>(null);
  const canvasRef = useRef<HTMLDivElement>(null);
  const canvasAreaRef = useRef<HTMLDivElement>(null);

  const addBlock = (id: string, x = 50, y = 50, customText = '') => {
    if (id === 'custom_image') {
      const input = document.createElement('input');
      input.type = 'file';
      input.accept = 'image/*';
      input.onchange = (e: any) => {
        const file = e.target.files?.[0];
        if (file) {
          const reader = new FileReader();
          reader.onload = () => {
            const newBlock: DesignerBlock = {
              Id: 'custom_image',
              X: x,
              Y: y,
              Width: 150,
              Height: 150,
              ColorHex: '#000000',
              FontSize: 12,
              FontFamily: 'Inter',
              Opacity: 1,
              IsBold: false,
              IsItalic: false,
              IsUnderlined: false,
              CustomText: '',
              IsHalfA4: isHalfA4,
              TextAlignment: 'Left',
              BorderRadius: 0,
              TableRows: 0,
              TableCols: 0,
              TableCellsJson: '',
              VisibilityCondition: '',
              ImagePath: reader.result as string
            };
            const nextBlocks = [...blocks, newBlock];
            setBlocks(nextBlocks);
            updateHistory(nextBlocks);
            setSelectedBlockIndex(blocks.length);
            setSelectedCellPos(null);
          };
          reader.readAsDataURL(file);
        }
      };
      input.click();
      return;
    }

    const defaultCells: TableCellData[] = [];
    if (id === 'table') {
      for (let r = 0; r < 3; r++) {
        for (let c = 0; c < 3; c++) {
          defaultCells.push({
            Row: r, Col: c, RowSpan: 1, ColSpan: 1, Text: '',
            BackgroundColor: 'Transparent', BorderColor: '#CCCCCC',
            BorderL: 1, BorderT: 1, BorderR: 1, BorderB: 1,
            BorderStyle: 'Solid', TextAlignment: 'Left'
          });
        }
      }
    }

    const isPlaceholder = id.startsWith('{') && id.endsWith('}');

    let width = 300;
    let height = 50;
    if (id === 'table') { width = 450; height = 120; }
    else if (id === 'rect' || id === 'circle' || id === 'triangle' || id === 'polygon') { width = 100; height = 100; }
    else if (id === 'line') { width = 600; height = 2; }
    else if (id === 'logo') { width = 150; height = 150; }

    const newBlock: DesignerBlock = {
      Id: id.startsWith('{') ? 'custom_text' : id,
      X: x,
      Y: y,
      Width: width,
      Height: height,
      ColorHex: (id === 'rect' || id === 'circle' || id === 'triangle' || id === 'polygon' || id === 'line') 
        ? (isDarkMode ? '#475569' : '#cbd5e1') 
        : '#000000',
      FontSize: isPlaceholder ? 11 : 16,
      FontFamily: 'Inter',
      Opacity: 1,
      IsBold: false,
      IsItalic: false,
      IsUnderlined: false,
      CustomText: customText || (id === 'custom_text' ? 'New Text' : (id.startsWith('logo') ? '{name}' : id.startsWith('{') ? id : '')),
      IsHalfA4: isHalfA4,
      TextAlignment: 'Left',
      BorderRadius: 0,
      TableRows: id === 'table' ? 3 : 0,
      TableCols: id === 'table' ? 3 : 0,
      TableCellsJson: JSON.stringify(defaultCells),
      TableColumnWidths: id === 'table' ? [150, 150, 150] : [],
      TableRowHeights: id === 'table' ? [40, 40, 40] : [],
      VisibilityCondition: '',
      BorderColorHex: (id === 'triangle' || id === 'polygon') ? '#000000' : 'Transparent',
      ShapeBorderThickness: (id === 'triangle' || id === 'polygon') ? 1 : 0,
      PolygonSides: id === 'triangle' ? 3 : (id === 'polygon' ? 5 : 0)
    };
    const nextBlocks = [...blocks, newBlock];
    setBlocks(nextBlocks);
    updateHistory(nextBlocks);
    setSelectedBlockIndex(blocks.length);
    setSelectedCellPos(null);
  };

  const updateHistory = (newBlocks: DesignerBlock[]) => {
    const newHistory = history.slice(0, historyIndex + 1);
    newHistory.push(newBlocks);
    setHistory(newHistory);
    setHistoryIndex(newHistory.length - 1);
  };

  const updateBlock = (index: number, updates: Partial<DesignerBlock>, skipHistory = false) => {
    const newBlocks = [...blocks];
    newBlocks[index] = { ...newBlocks[index], ...updates };
    setBlocks(newBlocks);
    if (!skipHistory) {
      updateHistory(newBlocks);
    }
  };

  const undo = () => {
    if (historyIndex > 0) {
      const prev = history[historyIndex - 1];
      setBlocks(prev);
      setHistoryIndex(historyIndex - 1);
    }
  };

  const redo = () => {
    if (historyIndex < history.length - 1) {
      const next = history[historyIndex + 1];
      setBlocks(next);
      setHistoryIndex(historyIndex + 1);
    }
  };

  useEffect(() => {
    if (blocks.length > 0 && history.length === 0) {
      setHistory([blocks]);
      setHistoryIndex(0);
    }
  }, [blocks]);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      const isInput = e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement;

      if (e.ctrlKey || e.metaKey) {
        if (e.key.toLowerCase() === 'z') {
          e.preventDefault();
          if (e.shiftKey) {
            redo();
          } else {
            undo();
          }
        }
        if (e.key.toLowerCase() === 'y') { e.preventDefault(); redo(); }
        return;
      }

      if (isInput) return;

      if (selectedBlockIndex !== null) {
        const block = blocks[selectedBlockIndex];
        const step = e.shiftKey ? 10 : 1;
        if (e.key === 'ArrowLeft') { e.preventDefault(); updateBlock(selectedBlockIndex, { X: block.X - step }); }
        if (e.key === 'ArrowRight') { e.preventDefault(); updateBlock(selectedBlockIndex, { X: block.X + step }); }
        if (e.key === 'ArrowUp') { e.preventDefault(); updateBlock(selectedBlockIndex, { Y: block.Y - step }); }
        if (e.key === 'ArrowDown') { e.preventDefault(); updateBlock(selectedBlockIndex, { Y: block.Y + step }); }
        if (e.key === 'Delete' || e.key === 'Backspace') {
          e.preventDefault();
          const newBlocks = blocks.filter((_, i) => i !== selectedBlockIndex);
          setBlocks(newBlocks);
          setSelectedBlockIndex(null);
          updateHistory(newBlocks);
        }
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [selectedBlockIndex, blocks, history, historyIndex]);

  useEffect(() => {
    if (view !== 'editor' || !canvasAreaRef.current) return;

    const currentArea = canvasAreaRef.current;
    const handleWheel = (e: WheelEvent) => {
      e.preventDefault();
      if (e.ctrlKey) {
        const delta = -e.deltaY;
        const factor = delta > 0 ? 1.1 : 0.9;
        setZoom(z => Math.min(2, Math.max(0.1, z * factor)));
      } else {
        setPanOffset(prev => ({
          x: prev.x - e.deltaX,
          y: prev.y - e.deltaY
        }));
      }
    };

    currentArea.addEventListener('wheel', handleWheel, { passive: false });
    return () => currentArea.removeEventListener('wheel', handleWheel);
  }, [view]);

  const getTableCells = (block: DesignerBlock): TableCellData[] => {
    try { return JSON.parse(block.TableCellsJson); } catch { return []; }
  };

  const updateTableCell = (blockIndex: number, row: number, col: number, updates: Partial<TableCellData>) => {
    const block = blocks[blockIndex];
    const cells = getTableCells(block);
    const cellIndex = cells.findIndex(c => c.Row === row && c.Col === col);
    if (cellIndex !== -1) {
      cells[cellIndex] = { ...cells[cellIndex], ...updates };
      updateBlock(blockIndex, { TableCellsJson: JSON.stringify(cells) });
    }
  };

  const addTableRow = (index: number) => {
    const block = blocks[index];
    const newRows = block.TableRows + 1;
    const cells = getTableCells(block);
    for (let c = 0; c < block.TableCols; c++) {
      cells.push({ Row: block.TableRows, Col: c, RowSpan: 1, ColSpan: 1, Text: '', BackgroundColor: 'Transparent', BorderColor: '#CCCCCC', BorderL: 1, BorderT: 1, BorderR: 1, BorderB: 1, BorderStyle: 'Solid', TextAlignment: 'Left' });
    }
    const rowHeight = 40;
    const newHeights = [...(block.TableRowHeights || [])];
    newHeights.push(rowHeight);
    updateBlock(index, { TableRows: newRows, TableCellsJson: JSON.stringify(cells), TableRowHeights: newHeights, Height: block.Height + rowHeight });
  };

  const removeTableRow = (index: number) => {
    const block = blocks[index];
    if (block.TableRows <= 1) return;

    const rowIndexToRemove = (selectedCellPos && selectedBlockIndex === index) ? selectedCellPos.row : block.TableRows - 1;
    const removedHeight = (block.TableRowHeights || [])[rowIndexToRemove] || 40;

    let cells = getTableCells(block);
    // Remove cells in that row and shift others up
    cells = cells.filter(c => c.Row !== rowIndexToRemove)
      .map(c => c.Row > rowIndexToRemove ? { ...c, Row: c.Row - 1 } : c);

    const newHeights = [...(block.TableRowHeights || [])];
    newHeights.splice(rowIndexToRemove, 1);

    updateBlock(index, {
      TableRows: block.TableRows - 1,
      TableCellsJson: JSON.stringify(cells),
      TableRowHeights: newHeights,
      Height: block.Height - removedHeight
    });
    setSelectedCellPos(null);
  };

  const addTableCol = (index: number) => {
    const block = blocks[index];
    const newCols = block.TableCols + 1;
    const cells = getTableCells(block);
    for (let r = 0; r < block.TableRows; r++) {
      cells.push({ Row: r, Col: block.TableCols, RowSpan: 1, ColSpan: 1, Text: '', BackgroundColor: 'Transparent', BorderColor: '#CCCCCC', BorderL: 1, BorderT: 1, BorderR: 1, BorderB: 1, BorderStyle: 'Solid', TextAlignment: 'Left' });
    }
    const colWidth = 100;
    const newWidths = [...(block.TableColumnWidths || [])];
    newWidths.push(colWidth);
    updateBlock(index, { TableCols: newCols, TableCellsJson: JSON.stringify(cells), TableColumnWidths: newWidths, Width: block.Width + colWidth });
  };

  const removeTableCol = (index: number) => {
    const block = blocks[index];
    if (block.TableCols <= 1) return;

    const colIndexToRemove = (selectedCellPos && selectedBlockIndex === index) ? selectedCellPos.col : block.TableCols - 1;
    const removedWidth = (block.TableColumnWidths || [])[colIndexToRemove] || 100;

    let cells = getTableCells(block);
    // Remove cells in that column and shift others left
    cells = cells.filter(c => c.Col !== colIndexToRemove)
      .map(c => c.Col > colIndexToRemove ? { ...c, Col: c.Col - 1 } : c);

    const newWidths = [...(block.TableColumnWidths || [])];
    newWidths.splice(colIndexToRemove, 1);

    updateBlock(index, {
      TableCols: block.TableCols - 1,
      TableCellsJson: JSON.stringify(cells),
      TableColumnWidths: newWidths,
      Width: block.Width - removedWidth
    });
    setSelectedCellPos(null);
  };

  const handleMouseDown = (e: React.MouseEvent, index: number, isCellClick = false) => {
    if (editingBlockIndex === index) return;
    e.stopPropagation();
    setSelectedBlockIndex(index);
    if (!isCellClick) setSelectedCellPos(null);
    const block = blocks[index];
    setDragging({ index, startX: e.clientX, startY: e.clientY, blockX: block.X, blockY: block.Y });
  };

  const handleMouseMove = (e: React.MouseEvent) => {
    if (blockResizing) {
      const deltaX = (e.clientX - blockResizing.startX) / zoom;
      const deltaY = (e.clientY - blockResizing.startY) / zoom;
      updateBlock(blockResizing.index, {
        Width: Math.max(10, Math.round(blockResizing.startWidth + deltaX)),
        Height: Math.max(10, Math.round(blockResizing.startHeight + deltaY))
      }, true);
      return;
    }
    if (resizing) {
      const delta = (resizing.type === 'col' ? e.clientX : e.clientY) - resizing.startPos;
      const block = blocks[resizing.blockIndex];
      const deltaPx = delta / zoom;
      const sizes = resizing.type === 'col' ? [...(block.TableColumnWidths || [])] : [...(block.TableRowHeights || [])];

      if (sizes[resizing.index]) {
        const newSize = Math.max(10, resizing.startSize + deltaPx);
        sizes[resizing.index] = newSize;
        const totalDelta = newSize - resizing.startSize;

        if (resizing.type === 'col') {
          updateBlock(resizing.blockIndex, {
            TableColumnWidths: sizes,
            Width: resizing.initialTotalSize + totalDelta
          }, true);
        } else {
          updateBlock(resizing.blockIndex, {
            TableRowHeights: sizes,
            Height: resizing.initialTotalSize + totalDelta
          }, true);
        }
      }
      return;
    }
    if (!dragging) return;
    const deltaX = (e.clientX - dragging.startX) / zoom;
    const deltaY = (e.clientY - dragging.startY) / zoom;
    updateBlock(dragging.index, { X: Math.round(dragging.blockX + deltaX), Y: Math.round(dragging.blockY + deltaY) }, true);
  };

  const handleMouseUp = () => {
    if (dragging || resizing || blockResizing) {
      updateHistory(blocks);
    }
    setDragging(null);
    setResizing(null);
    setBlockResizing(null);
  };

  const getDisplayText = (text: string, fallbackId?: string) => {
    let resolvedText = text;
    if (!resolvedText && fallbackId) {
      const isDesignItem = ['rect', 'circle', 'triangle', 'polygon', 'line', 'table', 'image', 'logo', 'custom_image'].includes(fallbackId);
      if (!isDesignItem) {
        resolvedText = `{${fallbackId}}`;
      }
    }
    if (!showDummyData) return resolvedText;
    let newText = resolvedText;
    Object.keys(DUMMY_DATA).forEach(key => { newText = newText.replace(`{${key}}`, DUMMY_DATA[key]); });
    return newText;
  };

  const fetchDbMetrics = async () => {
    try {
      const { data: settingsData, error: settingsError } = await supabase
        .from('system_settings')
        .select('value')
        .eq('key', 'is_digitalocean_enabled')
        .single();
      if (!settingsError && settingsData) {
        setIsDigitalOceanEnabled(settingsData.value === 'true');
      }

      const { data: keysData, error: keysError } = await supabase
        .from('activation_keys')
        .select('cloud_storage_limit_gb')
        .eq('cloud_sync_enabled', true);
      if (!keysError && keysData) {
        const totalAllocatedGb = keysData.reduce((sum: number, item: any) => sum + parseFloat(item.cloud_storage_limit_gb || '0'), 0);
        const remainingMb = 500 - (totalAllocatedGb * 1024);
        setRemainingFreeSpaceMb(remainingMb);
      }
    } catch (e) {
      console.error('Error fetching db metrics:', e);
    }
  };

  const handleToggleDigitalOcean = async () => {
    const nextValue = !isDigitalOceanEnabled;
    try {
      const { error } = await supabase
        .from('system_settings')
        .update({ value: nextValue ? 'true' : 'false' })
        .eq('key', 'is_digitalocean_enabled');
      if (error) {
        alert('Failed to update system setting: ' + error.message);
      } else {
        setIsDigitalOceanEnabled(nextValue);
        alert(`Successfully ${nextValue ? 'enabled' : 'disabled'} DigitalOcean PocketBase storage!`);
        await fetchDbMetrics();
      }
    } catch (e: any) {
      alert('Error updating system setting: ' + e.message);
    }
  };

  const fetchTemplates = async () => {
    const { data: templates } = await supabase.from('cloud_templates').select('*').order('created_at', { ascending: false });
    if (templates) setSavedTemplates(templates);

    const { data: keys } = await supabase.from('activation_keys').select('id');
    if (keys) setTotalKeys(keys.length);

    const { data: updates } = await supabase.from('app_updates').select('version, update_type').order('created_at', { ascending: false }).limit(1);
    if (updates && updates.length > 0) {
      setLatestVersion(updates[0].version);
      setLatestVersionType(updates[0].update_type);
    }
    await fetchDbMetrics();
  };

  useEffect(() => {
    fetchTemplates();
  }, [view]);

  const loadTemplate = (t: any) => {
    let loadedBlocks: DesignerBlock[] = [];
    try {
      const data = JSON.parse(t.json_data);
      if (data.blocks) {
        loadedBlocks = data.blocks;
      } else {
        loadedBlocks = data; // Fallback for old format
      }
    } catch {
      loadedBlocks = [];
    }
    setBlocks(loadedBlocks);
    setHistory([loadedBlocks]);
    setHistoryIndex(0);
    setTemplateName(t.name);
    setAuthorName(t.author);
    setIsHalfA4(t.is_half_a4);
    setCurrentTemplateId(t.id);
    setView('editor');
  };

  const deleteTemplate = async (id: string) => {
    if (!confirm('Delete this template?')) return;
    const { error } = await supabase.from('cloud_templates').delete().eq('id', id);
    if (!error) fetchTemplates();
  };

  const saveToCloud = async (isPublishedOverride?: boolean) => {
    if (!templateName) { alert('Name required'); return; }
    setIsSaving(true);
    try {
      // Determine final published status
      let finalPublished = isPublishedOverride;
      if (finalPublished === undefined) {
        // If not specified, check if current template was already published
        try {
          const t = savedTemplates.find(st => st.id === currentTemplateId);
          if (t) finalPublished = JSON.parse(t.json_data).is_published;
        } catch { finalPublished = false; }
      }

      const templateData = {
        blocks: blocks,
        is_published: !!finalPublished
      };

      const payload = {
        name: templateName,
        author: authorName,
        json_data: JSON.stringify(templateData),
        is_half_a4: isHalfA4,
        is_published: !!finalPublished
      };

      let error;
      if (currentTemplateId) {
        const { error: err } = await supabase.from('cloud_templates').update(payload).eq('id', currentTemplateId);
        error = err;
      } else {
        const { error: err, data } = await supabase.from('cloud_templates').insert([payload]).select();
        error = err;
        if (data && data[0]) setCurrentTemplateId(data[0].id);
      }

      if (error) throw error;
      await fetchTemplates(); // Refresh local list
      alert(finalPublished ? 'Template published to Cloud Server!' : 'Template saved as draft.');
    } catch (err: any) { alert(err.message); }
    finally { setIsSaving(false); }
  };

  const publishTemplate = async (t: any) => {
    setIsPublishing(t.id);
    try {
      let data;
      try {
        const rawData = typeof t.json_data === 'string' ? JSON.parse(t.json_data) : t.json_data;
        // CRITICAL FIX: If data is an array (old format), wrap it in an object
        // because arrays don't stringify named properties in JSON.
        if (Array.isArray(rawData)) {
          data = { blocks: rawData, is_published: true };
        } else {
          data = { ...rawData, is_published: true };
        }
      } catch { data = { blocks: [], is_published: true }; }

      const { error, data: updatedData } = await supabase.from('cloud_templates').update({
        json_data: JSON.stringify(data),
        is_published: true
      }).eq('id', t.id).select();

      console.log("Publish result:", { error, updatedData });

      if (!error) {
        await fetchTemplates();
      } else {
        alert("Publish Error: " + error.message);
      }
    } catch (err: any) { alert(err.message); }
    finally { setIsPublishing(null); }
  };

  const handleAiGenerate = async () => {
    if (!aiPrompt) return;
    setIsAiGenerating(true);

    try {
      const { data, error } = await supabase.functions.invoke('generate-template', {
        body: { prompt: aiPrompt }
      });

      if (error) throw new Error(`Edge Function Error: ${error.message}`);
      if (!data || !data.choices || data.choices.length === 0) throw new Error("Invalid response from Edge Function");

      let generatedText = data.choices[0].message.content.trim();

      // Clean up markdown and extract just the JSON array
      const startIndex = generatedText.indexOf('[');
      const endIndex = generatedText.lastIndexOf(']');
      if (startIndex !== -1 && endIndex !== -1 && endIndex > startIndex) {
        generatedText = generatedText.substring(startIndex, endIndex + 1);
      }
      
      const aiBlocks = JSON.parse(generatedText);

      // Basic validation to ensure we got an array
      if (Array.isArray(aiBlocks)) {
        setBlocks(aiBlocks);
        setHistoryIndex(prev => {
          const newHistory = history.slice(0, prev + 1);
          newHistory.push(aiBlocks);
          setHistory(newHistory);
          return newHistory.length - 1;
        });
      }
    } catch (error: any) {
      console.error("AI Generation failed:", error);
      alert(`AI Generation failed: ${error.message || error}`);
    } finally {
      setIsAiGenerating(false);
      setAiPrompt('');
    }
  };

  const handleCanvaImport = async (file: File) => {
    const pdfjs = await loadPdfJs();
    const fileReader = new FileReader();
    fileReader.onload = async () => {
      try {
        const typedarray = new Uint8Array(fileReader.result as ArrayBuffer);
        const loadingTask = pdfjs.getDocument({ data: typedarray });
        const pdf = await loadingTask.promise;
        const page = await pdf.getPage(1);

        // Render page to canvas to get background image (Base64)
        const viewport = page.getViewport({ scale: 2.0 }); // 2x scale for crisp image
        const canvas = document.createElement('canvas');
        const context = canvas.getContext('2d');
        if (!context) return;
        canvas.width = viewport.width;
        canvas.height = viewport.height;
        await page.render({ canvasContext: context, viewport }).promise;
        const base64Bg = canvas.toDataURL('image/png');

        // Extract text items to find placeholders
        const textContent = await page.getTextContent();
        const items: any[] = textContent.items;

        const resolvedBlocks: any[] = [];
        const detectedPlaceholders: string[] = [];

        const placeholders = new Set([
          "memo_id", "date", "customer_name", "customer_phone", "customer_address",
          "brand", "model", "product_name", "serial_number", "accessories", "issue", "diagnostics",
          "cost", "technician_name", "name", "address", "phone", "terms"
        ]);

        const synonymMap: any = {
          "company_name": "name",
          "company_address": "address",
          "company_phone": "phone",
          "description": "issue",
          "issue_description": "issue",
          "order_id": "memo_id",
          "order_number": "memo_id",
          "id": "memo_id",
          "order_date": "date",
          "memo_date": "date",
          "customer": "customer_name",
          "phone_number": "customer_phone",
          "contact": "customer_phone",
          "client_address": "customer_address",
          "device_brand": "brand",
          "device_model": "model",
          "device": "model",
          "product_name": "product_name",
          "device_name": "product_name",
          "technician": "technician_name",
          "tech": "technician_name",
          "estimated_cost": "cost",
          "price": "cost",
          "amount": "cost"
        };

        const getIdealWidth = (name: string) => {
          switch (name) {
            case "memo_id": return 150;
            case "date": return 150;
            case "customer_name": return 250;
            case "customer_phone": return 180;
            case "customer_address": return 350;
            case "brand": return 150;
            case "model": return 200;
            case "product_name": return 200;
            case "serial_number": return 200;
            case "accessories": return 350;
            case "issue": return 450;
            case "diagnostics": return 450;
            case "cost": return 150;
            case "technician_name": return 220;
            case "name": return 300;
            case "address": return 400;
            case "phone": return 220;
            case "terms": return 600;
            default: return 200;
          }
        };

        // PDF dimensions
        const pdfW = page.view[2];
        const pdfH = page.view[3];
        const wpfWidth = 794.0;
        const wpfHeight = isHalfA4 ? 561.0 : Math.round(wpfWidth * (pdfH / pdfW));

        // Background Image Block
        const bgBlock = {
          Id: "image",
          X: 0,
          Y: 0,
          Width: wpfWidth,
          Height: wpfHeight,
          ImagePath: base64Bg,
          IsHalfA4: isHalfA4,
          Opacity: 1.0,
          FontSize: 12,
          FontFamily: "Inter",
          ColorHex: "#000000",
          CustomText: "",
          TableCellsJson: "",
          TableColumnWidths: "",
          TableRowHeights: "",
          FormattedTextXaml: "",
          TextAlignment: "Left"
        };
        resolvedBlocks.push(bgBlock);

        // Loop through all text items to search for placeholders
        const regex = /\{\{?\s*([a-zA-Z0-9_]+)\s*\}?\}/i;
        items.forEach((item: any) => {
          const match = regex.exec(item.str);
          if (match) {
            let placeholderName = match[1].toLowerCase();
            const resolvedName = synonymMap[placeholderName] || placeholderName;

            if (placeholders.has(resolvedName) && !detectedPlaceholders.includes(resolvedName)) {
              detectedPlaceholders.push(resolvedName);

              const pdfLeft = item.transform[4];
              const pdfBottom = item.transform[5];
              const pdfWidthVal = item.width;
              const pdfHeightVal = item.height;
              const pdfTop = pdfBottom + pdfHeightVal;

              const wpfLeft = (pdfLeft / pdfW) * wpfWidth;
              const wpfTop = ((pdfH - pdfTop) / pdfH) * wpfHeight;
              
              const idealW = getIdealWidth(resolvedName);
              let wpfWVal = Math.max((pdfWidthVal / pdfW) * wpfWidth, idealW);
              if (wpfLeft + wpfWVal > wpfWidth) {
                wpfWVal = wpfWidth - wpfLeft - 10;
              }

              const fontSize = item.transform[0];
              const wpfFontSize = Math.max(fontSize * (wpfWidth / pdfW), 6);
              const wpfHVal = Math.max((pdfHeightVal / pdfH) * wpfHeight, wpfFontSize * 1.5);

              // 1. Calculate boundaries of background text eraser
              const x1 = (pdfLeft / pdfW) * canvas.width;
              const w1 = (pdfWidthVal / pdfW) * canvas.width;
              const y_baseline = ((pdfH - pdfBottom) / pdfH) * canvas.height;
              const h_actual = (fontSize / pdfH) * canvas.height;
              
              // Tightened vertical bounds to prevent spilling into white areas above/below grey rows
              const h1 = h_actual * 1.15; // Clean coverage for ascenders/descenders without overflow
              const y1 = y_baseline - h_actual * 0.9; // Spans 0.9x above baseline, 0.25x below baseline

              // 2. Sample background color at the vertical center of the text, 10px to the left
              let sampledHex = "Transparent";
              try {
                const sampleX = Math.max(0, x1 - 10);
                const sampleY = y_baseline - (h_actual / 2);
                const imgData = context.getImageData(sampleX, sampleY, 1, 1).data;
                const r = imgData[0];
                const g = imgData[1];
                const b = imgData[2];
                sampledHex = "#" + ((1 << 24) + (r << 16) + (g << 8) + b).toString(16).slice(1);
                
                // Erase placeholder on canvas background using the sampled color
                context.fillStyle = `rgb(${r}, ${g}, ${b})`;
                context.fillRect(x1 - 6, y1, w1 + h_actual * 1.5 + 12, h1);
              } catch (err) {
                console.error("Sampling/Erase failed:", err);
              }

              // 3. Push placeholder block with BackgroundColorHex initialized to sampledHex
              resolvedBlocks.push({
                Id: resolvedName,
                X: Math.round(wpfLeft),
                Y: Math.round(wpfTop + wpfFontSize * 0.45),
                Width: Math.round(wpfWVal),
                Height: Math.round(wpfHVal),
                FontSize: Math.round(wpfFontSize),
                FontFamily: "Inter",
                ColorHex: "#000000",
                BackgroundColorHex: sampledHex, // Match the grey row background perfectly!
                IsBold: false,
                IsItalic: false,
                IsUnderlined: false,
                ImagePath: "",
                CustomText: "",
                IsHalfA4: isHalfA4,
                TextAlignment: "Left",
                Opacity: 1.0,
                TableCellsJson: "",
                TableColumnWidths: "",
                TableRowHeights: "",
                FormattedTextXaml: ""
              });
            }
          }
        });

        // Re-generate base64 background after erasures
        bgBlock.ImagePath = canvas.toDataURL('image/png');

        setBlocks(resolvedBlocks);
        setHistory([resolvedBlocks]);
        setHistoryIndex(0);

        alert(`Successfully imported Canva PDF!\n\nExtracted ${detectedPlaceholders.length} placeholder fields.`);
      } catch (err: any) {
        alert("Error parsing PDF: " + err.message);
      }
    };
    fileReader.readAsArrayBuffer(file);
  };

  const selectedBlock = selectedBlockIndex !== null ? blocks[selectedBlockIndex] : null;
  const isImg = selectedBlock ? (selectedBlock.Id === 'image' || selectedBlock.Id === 'logo' || selectedBlock.Id === 'custom_image') : false;
  const selectedCell = (selectedBlock && selectedCellPos) ? getTableCells(selectedBlock).find(c => c.Row === selectedCellPos.row && c.Col === selectedCellPos.col) : null;

  const MiniPreview = ({ json_data, isHalfA4 }: { json_data: string, isHalfA4: boolean }) => {
    try {
      const data = JSON.parse(json_data);
      const previewBlocks: DesignerBlock[] = data.blocks || data;
      const previewZoom = 0.25;

      return (
        <div className={`paper ${isHalfA4 ? 'half-a4' : 'a4'}`} style={{
          transform: `scale(${previewZoom})`,
          transformOrigin: 'top left',
          pointerEvents: 'none',
          boxShadow: 'none',
          position: 'absolute',
          top: 0, left: 0
        }}>
          {previewBlocks.map((block, idx) => {
            const isImg = block.Id === 'image' || block.Id === 'logo' || block.Id === 'custom_image';
            const isShape = block.Id === 'triangle' || block.Id === 'polygon';
            return (
              <div key={idx} style={{
                position: 'absolute',
                left: block.X, top: block.Y, width: block.Width, height: block.Height,
                backgroundColor: (block.Id === 'rect' || block.Id === 'circle' || block.Id === 'line') ? (block.ColorHex || 'transparent') : 'transparent',
                color: block.ColorHex, fontSize: block.FontSize, fontFamily: block.FontFamily,
                fontWeight: block.IsBold ? 'bold' : 'normal',
                border: (block.Id === 'rect' || block.Id === 'circle') && block.ShapeBorderThickness && block.BorderColorHex && block.BorderColorHex !== 'Transparent'
                  ? `${block.ShapeBorderThickness}px solid ${block.BorderColorHex}`
                  : (block.Id === 'rect' ? `1px solid ${block.ColorHex}` : 'none'),
                borderRadius: block.Id === 'circle' ? '50%' : `${block.BorderRadius || 0}px`,
                opacity: block.Opacity,
              }}>
                {isImg ? (
                  block.ImagePath ? (
                    <img src={block.ImagePath} style={{ width: '100%', height: '100%', objectFit: 'contain' }} alt="" />
                  ) : '🖼️'
                ) : isShape ? (
                  <svg width="100%" height="100%" viewBox="0 0 100 100" preserveAspectRatio="none">
                    <polygon
                      points={getPolygonPoints(block.Id === 'triangle' ? 3 : (block.PolygonSides || 5))}
                      fill={block.ColorHex === 'Transparent' ? 'none' : (block.ColorHex || '#CCCCCC')}
                      stroke={block.BorderColorHex === 'Transparent' ? 'none' : (block.BorderColorHex || 'transparent')}
                      strokeWidth={block.ShapeBorderThickness || 0}
                      vectorEffect="non-scaling-stroke"
                    />
                  </svg>
                ) : block.Id === 'line' ? (
                  <div style={{ width: '100%', height: block.Height, backgroundColor: block.ColorHex }} />
                ) : (
                  block.Id !== 'rect' && block.Id !== 'circle' && block.Id !== 'table' && '■'
                )}
              </div>
            );
          })}
        </div>
      );
    } catch { return <Layout size={48} opacity={0.2} />; }
  };  function TemplateCard({ t }: { t: any }) {
    const isPublished = !!t.is_published;
    const loading = isPublishing === t.id;

    return (
      <div className={`template-card ${loading ? 'animate-pulse' : ''}`}>
        <div className="card-preview" style={{ overflow: 'hidden' }}>
          <div style={{ position: 'relative', width: 794 * 0.25, height: (t.is_half_a4 ? 561 : 1123) * 0.25 }}>
            <MiniPreview json_data={t.json_data} isHalfA4={t.is_half_a4} />
          </div>
          {isPublished ? <span className="badge-published">Published</span> : <span className="badge-draft">Draft</span>}
          {loading && <div className="card-loading-overlay"><div className="loader-small" /></div>}
        </div>
        <div className="card-info">
          <h3 className="template-title">{t.name}</h3>
          <p className="template-meta">By {t.author} • {new Date(t.created_at).toLocaleDateString()}</p>
          <div className="card-actions">
            <button className="btn-small" onClick={() => loadTemplate(t)} disabled={!!isPublishing}>Edit</button>
            {!isPublished && (
              <button className="btn-small" style={{ color: '#10b981' }} onClick={() => publishTemplate(t)} disabled={!!isPublishing}>
                {loading ? 'Publishing...' : 'Publish'}
              </button>
            )}
            <button className="btn-small" onClick={() => deleteTemplate(t.id)} style={{ color: '#ff4444' }} disabled={!!isPublishing}>Delete</button>
          </div>
        </div>
      </div>
    );
  }

  const renderDashboardView = () => {
    return (
      <div className="dashboard-hub">
        <div className="welcome-banner animate-in">
          <h1 className="welcome-title">Welcome to Cloud Admin Console</h1>
          <p className="welcome-subtitle">
            Manage your business document templates, user licenses, and application updates from one premium unified control room.
          </p>
        </div>

        {/* Database Provisioning & Storage Provider Controls */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: 24 }} className="animate-in">
          {/* Allocation Progress Bar Card */}
          <div className="action-glass-card" style={{ display: 'flex', flexDirection: 'column', gap: 16, padding: 24 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <span style={{ textTransform: 'uppercase', fontSize: 11, fontWeight: 700, color: 'var(--text-muted)' }}>
                Free Tier Database Allocation
              </span>
              <span style={{ fontSize: 11, fontWeight: 'bold', color: 'var(--primary)' }}>
                500 MB Shared Limit
              </span>
            </div>
            
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '1.5rem', fontWeight: 800, marginBottom: 8 }}>
                <span>{Math.max(0, 500 - remainingFreeSpaceMb).toFixed(1)} MB</span>
                <span style={{ color: 'var(--text-muted)', fontSize: '1rem', alignSelf: 'flex-end', marginBottom: 3 }}>/ 500.0 MB</span>
              </div>
              
              <div style={{ width: '100%', height: 12, background: 'var(--outline)', borderRadius: 6, overflow: 'hidden' }}>
                <div 
                  style={{ 
                    width: `${Math.min(100, Math.max(2, ((500 - remainingFreeSpaceMb) / 500) * 100))}%`, 
                    height: '100%', 
                    background: (500 - remainingFreeSpaceMb) > 450 ? '#ef4444' : 'linear-gradient(90deg, var(--primary) 0%, #60a5fa 100%)',
                    borderRadius: 6,
                    transition: 'width 0.4s ease'
                  }}
                ></div>
              </div>
            </div>
            
            <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)', lineHeight: 1.4 }}>
              {isDigitalOceanEnabled 
                ? "✓ DigitalOcean PocketBase droplet is enabled. Supabase allocation capacity constraints bypassed."
                : `Remaining capacity: ${remainingFreeSpaceMb.toFixed(1)} MB. Allocations on new/edited activation keys are checked against this remaining free pool.`
              }
            </span>
          </div>

          {/* Database Provider details & Toggle switch */}
          <div className="action-glass-card" style={{ display: 'flex', flexDirection: 'column', gap: 16, padding: 24 }}>
            <div>
              <span style={{ textTransform: 'uppercase', fontSize: 11, fontWeight: 700, color: 'var(--text-muted)' }}>
                Database Storage Provider
              </span>
              <h3 style={{ margin: '6px 0 0 0', fontSize: '1.25rem', color: isDigitalOceanEnabled ? '#10b981' : 'var(--primary)', fontWeight: 800 }}>
                {isDigitalOceanEnabled ? '🚀 DigitalOcean PocketBase' : '⚡ Supabase Free Shared Database'}
              </h3>
              <p style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginTop: 4, lineHeight: 1.4 }}>
                {isDigitalOceanEnabled 
                  ? 'Running on high-performance DigitalOcean droplets with unlimited SSD storage space. Large tier client keys (500MB+) can now be generated.'
                  : 'Utilizing Supabase free pool. All sync plans >= 500MB are blocked from checkout until DigitalOcean PocketBase is enabled.'
                }
              </p>
            </div>

            <button 
              className="action-btn" 
              onClick={handleToggleDigitalOcean}
              style={{ 
                marginTop: 'auto', 
                height: 42, 
                fontWeight: 'bold',
                background: isDigitalOceanEnabled ? 'rgba(239, 68, 68, 0.15)' : 'rgba(16, 185, 129, 0.15)',
                color: isDigitalOceanEnabled ? '#ef4444' : '#10b981',
                border: isDigitalOceanEnabled ? '1px solid rgba(239,68,68,0.3)' : '1px solid rgba(16,185,129,0.3)',
                cursor: 'pointer',
                borderRadius: 8
              }}
            >
              {isDigitalOceanEnabled ? '🔌 Switch Back to Supabase Free Tier' : '⚡ Enable DigitalOcean PocketBase Storage'}
            </button>
          </div>
        </div>

        <div className="metrics-row">
          <div className="metric-glass-card">
            <div className="metric-icon-box templates">
              <FileCode size={24} />
            </div>
            <div className="metric-info">
              <span className="metric-val">{savedTemplates.length}</span>
              <span className="metric-lbl">Total Templates</span>
            </div>
          </div>

          <div className="metric-glass-card">
            <div className="metric-icon-box keys">
              <Key size={24} />
            </div>
            <div className="metric-info">
              <span className="metric-val">{totalKeys}</span>
              <span className="metric-lbl">Active Keys</span>
            </div>
          </div>

          <div className="metric-glass-card">
            <div className="metric-icon-box updates">
              <Cloud size={24} />
            </div>
            <div className="metric-info">
              <span className="metric-val">v{latestVersion}</span>
              <span className="metric-lbl">Latest Update ({latestVersionType})</span>
            </div>
          </div>
        </div>

        <div className="quick-actions-grid">
          <div className="action-glass-card">
            <div className="action-header">
              <div className="action-icon">
                <Layout size={24} />
              </div>
              <h3 className="action-title">Templates Manager</h3>
            </div>
            <p className="action-description">
              Craft custom service memos, invoices, and diagnostic grids using our rich canvas drag-and-drop designer. Generate beautiful structural ideas instantly using AI assistance.
            </p>
            <div className="action-footer" style={{ display: 'flex', gap: 12 }}>
              <button className="action-btn btn-secondary" onClick={() => {
                const input = document.createElement('input');
                input.type = 'file';
                input.accept = 'application/pdf';
                input.onchange = async (e: any) => {
                  const file = e.target.files?.[0];
                  if (file) {
                    setBlocks([]);
                    const nameWithoutExt = file.name.replace(/\.[^/.]+$/, "");
                    setTemplateName(nameWithoutExt);
                    setCurrentTemplateId(null);
                    setView('editor');
                    await handleCanvaImport(file);
                  }
                };
                input.click();
              }} style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8, background: 'rgba(255,255,255,0.08)', color: 'var(--text-primary)' }}>
                <FileCode size={16} /> Import Canva PDF
              </button>
              <button className="action-btn" onClick={() => setView('templates')}>
                Manage Templates
              </button>
            </div>
          </div>

          <div className="action-glass-card">
            <div className="action-header">
              <div className="action-icon">
                <Key size={24} />
              </div>
              <h3 className="action-title">Manage Licenses</h3>
            </div>
            <p className="action-description">
              Generate new security keys, manage maximum allowed seats, track local machine signatures, and revoke active customer installations in real time.
            </p>
            <div className="action-footer">
              <button className="action-btn" onClick={() => setView('keys')}>
                Manage Keys
              </button>
            </div>
          </div>

          <div className="action-glass-card">
            <div className="action-header">
              <div className="action-icon">
                <Cloud size={24} />
              </div>
              <h3 className="action-title">App Updates</h3>
            </div>
            <p className="action-description">
              Publish new minor software releases, major feature updates, or mandatory patches. Clients automatically download packages in the background while active.
            </p>
            <div className="action-footer">
              <button className="action-btn" onClick={() => setView('updates')}>
                Deploy Updates
              </button>
            </div>
          </div>
        </div>
      </div>
    );
  };

  const renderTemplatesView = () => {
    return (
      <div className="dashboard-area">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 40 }}>
          <div>
            <h1 className="headline-lg">Templates Manager</h1>
            <p className="caption">{savedTemplates.length} Templates total</p>
          </div>
          <div style={{ display: 'flex', gap: 12 }}>
            <button className="btn btn-secondary" onClick={() => {
              const input = document.createElement('input');
              input.type = 'file';
              input.accept = 'application/pdf';
              input.onchange = async (e: any) => {
                const file = e.target.files?.[0];
                if (file) {
                  setBlocks([]);
                  const nameWithoutExt = file.name.replace(/\.[^/.]+$/, "");
                  setTemplateName(nameWithoutExt);
                  setCurrentTemplateId(null);
                  setView('editor');
                  await handleCanvaImport(file);
                }
              };
              input.click();
            }} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <FileCode size={18} /> Import Canva PDF
            </button>
            <button className="btn btn-primary" onClick={() => {
              setBlocks([]);
              setHistory([[]]);
              setHistoryIndex(0);
              setTemplateName('New Template');
              setCurrentTemplateId(null);
              setView('editor');
            }}>
              <Plus size={18} /> Create New Template
            </button>
          </div>
        </div>

        <div className="dashboard-section">
          <h2 className="section-title published"><Cloud size={18} /> Published to Cloud</h2>
          <div className="template-grid">
            {savedTemplates.filter(t => t.is_published).map(t => (
              <TemplateCard key={t.id} t={t} />
            ))}
          </div>
        </div>

        <div className="dashboard-section" style={{ marginTop: 40 }}>
          <h2 className="section-title drafts"><Save size={18} /> Drafts & In-Progress</h2>
          <div className="template-grid">
            {savedTemplates.filter(t => !t.is_published).map(t => (
              <TemplateCard key={t.id} t={t} />
            ))}
          </div>
        </div>
      </div>
    );
  };

  if (view !== 'editor') {
    return (
      <div style={{ display: 'flex', height: '100vh', width: '100vw', background: 'var(--background)', overflow: 'hidden' }}>
        {/* Persistent Left Sidebar */}
        <aside className="sidebar" style={{ width: 280, flexShrink: 0 }}>
          <div className="headline-lg" style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <Sparkles size={22} style={{ color: 'var(--primary)' }} />
            <span>Cloud Admin</span>
          </div>
          <p className="caption" style={{ marginBottom: 24 }}>System Console</p>

          <nav style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <button className={`btn ${view === 'dashboard' ? 'btn-primary' : 'btn-secondary'}`} style={{ justifyContent: 'flex-start' }} onClick={() => setView('dashboard')}>
              <Layout size={18} /> Dashboard Overview
            </button>
            <button className={`btn ${view === 'templates' ? 'btn-primary' : 'btn-secondary'}`} style={{ justifyContent: 'flex-start' }} onClick={() => setView('templates')}>
              <FileCode size={18} /> Templates Manager
            </button>
            <button className={`btn ${view === 'keys' ? 'btn-primary' : 'btn-secondary'}`} style={{ justifyContent: 'flex-start' }} onClick={() => setView('keys')}>
              <Key size={18} /> Manage Licenses
            </button>
            <button className={`btn ${view === 'updates' ? 'btn-primary' : 'btn-secondary'}`} style={{ justifyContent: 'flex-start' }} onClick={() => setView('updates')}>
              <Cloud size={18} /> App Updates
            </button>
          </nav>

          <div style={{ marginTop: 'auto', display: 'flex', flexDirection: 'column', gap: 12 }}>
            <div className="toggle-switch" onClick={() => setIsDarkMode(!isDarkMode)}>
              <span>Dark Mode</span>
              {isDarkMode ? <Sparkles size={16} /> : <Cloud size={16} />}
            </div>
          </div>
        </aside>

        {/* View Main Content Area */}
        <main style={{ flex: 1, overflowY: 'auto', background: 'var(--canvas-bg)', position: 'relative' }}>
          {view === 'dashboard' && renderDashboardView()}
          {view === 'templates' && renderTemplatesView()}
          {view === 'keys' && <KeyManager isDarkMode={isDarkMode} />}
          {view === 'updates' && <UpdateManager isDarkMode={isDarkMode} />}
        </main>
      </div>
    );
  }

  return (
    <div className="admin-layout" onMouseMove={handleMouseMove} onMouseUp={handleMouseUp}>
      {/* Sidebar */}
      <aside className="sidebar">
        <button className="btn-small" onClick={() => setView('templates')} style={{ marginBottom: 12, width: 'fit-content' }}>← Back to Console</button>
        <div className="headline-lg">Editor</div>
        <input className="input-field headline-lg" style={{ background: 'transparent', border: 'none', padding: 0 }} value={templateName} onChange={e => setTemplateName(e.target.value)} />

        <div className="prop-group">
          <span className="caption">Elements (Drag)</span>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
            {TOOLBOX_ITEMS.map(item => (
              <div key={item.id} className="toolbox-item" draggable onDragStart={e => e.dataTransfer.setData('blockId', item.id)} onClick={() => addBlock(item.id)}>
                {item.icon} <span style={{ marginLeft: 6 }}>{item.name}</span>
              </div>
            ))}
          </div>
        </div>

        <div className="prop-group">
          <span className="caption">Placeholders (Drag)</span>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6 }}>
            {PRESET_FIELDS.map(field => (
              <div key={field.id} className="toolbox-item placeholder-item" draggable onDragStart={e => e.dataTransfer.setData('blockId', field.field)} onClick={() => addBlock(field.field)}>
                {field.icon} <span style={{ marginLeft: 6 }}>{field.name}</span>
              </div>
            ))}
          </div>
        </div>

        <div className="prop-group">
          <span className="caption">Preview Settings</span>
          <div className="toggle-switch" onClick={() => setIsDarkMode(!isDarkMode)}><span>Dark Mode</span>{isDarkMode ? <Sparkles size={16} /> : <Cloud size={16} />}</div>
          <div className="toggle-switch" onClick={() => setIsHalfA4(!isHalfA4)}><span>{isHalfA4 ? 'Half A4' : 'Full A4'}</span><Layout size={16} /></div>
          <div className="toggle-switch" onClick={() => setShowDummyData(!showDummyData)}><span>Live Preview</span>{showDummyData ? <Eye size={16} /> : <EyeOff size={16} />}</div>
          <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
            <button className="btn btn-secondary" style={{ flex: 1 }} onClick={() => setZoom(Math.max(0.1, zoom - 0.1))}>-</button>
            <span style={{ alignSelf: 'center', fontSize: 12, fontWeight: 'bold' }}>{Math.round(zoom * 100)}%</span>
            <button className="btn btn-secondary" style={{ flex: 1 }} onClick={() => setZoom(Math.min(2, zoom + 0.1))}>+</button>
          </div>
        </div>

        <div className="prop-group">
          <span className="caption">Canva Integration</span>
          <button className="btn btn-secondary" style={{ width: '100%', gap: 8, justifyContent: 'center' }} onClick={() => {
            const input = document.createElement('input');
            input.type = 'file';
            input.accept = 'application/pdf';
            input.onchange = async (e: any) => {
              const file = e.target.files?.[0];
              if (file) handleCanvaImport(file);
            };
            input.click();
          }}>
            <FileCode size={18} /> Import Canva PDF
          </button>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 'auto' }}>
          <button className="btn btn-secondary" onClick={() => saveToCloud()} disabled={isSaving}><Save size={18} /> {isSaving ? 'Saving...' : 'Save Changes'}</button>
          <button className="btn btn-primary" onClick={() => saveToCloud(true)} disabled={isSaving}><Cloud size={18} /> {isSaving ? 'Publishing...' : 'Publish to Cloud'}</button>
        </div>
      </aside>

      {/* Canvas */}
      <main className="canvas-area" ref={canvasAreaRef} onDragOver={e => e.preventDefault()} onDrop={e => {
        const id = e.dataTransfer.getData('blockId');
        const rect = canvasRef.current?.getBoundingClientRect();
        if (rect) addBlock(id, (e.clientX - rect.left) / zoom, (e.clientY - rect.top) / zoom);
      }} onMouseDown={() => { setSelectedBlockIndex(null); setEditingBlockIndex(null); setSelectedCellPos(null); }}>
        <div className={`paper ${isHalfA4 ? 'half-a4' : 'a4'}`} style={{ transform: `translate(${panOffset.x}px, ${panOffset.y}px) scale(${zoom})`, transformOrigin: 'center center' }} ref={canvasRef} onMouseDown={e => e.stopPropagation()}>
          {blocks.map((block, index) => {
            const isImg = block.Id === 'image' || block.Id === 'logo' || block.Id === 'custom_image';
            const isShape = block.Id === 'triangle' || block.Id === 'polygon';
            return (
              <div key={index} className={`designer-block ${selectedBlockIndex === index ? 'selected' : ''}`} style={{
                left: block.X, top: block.Y, width: block.Width, height: block.Height,
                color: block.ColorHex, fontSize: block.FontSize, fontFamily: block.FontFamily,
                fontWeight: block.IsBold ? 'bold' : 'normal', fontStyle: block.IsItalic ? 'italic' : 'normal',
                textDecoration: block.IsUnderlined ? 'underline' : 'none', opacity: block.Opacity,
                display: 'flex', alignItems: 'center', justifyContent: block.TextAlignment === 'Center' ? 'center' : (block.TextAlignment === 'Right' ? 'flex-end' : 'flex-start'),
                cursor: editingBlockIndex === index ? 'text' : (dragging?.index === index ? 'grabbing' : 'grab'),
                border: (block.Id === 'rect' || block.Id === 'circle') && block.ShapeBorderThickness && block.BorderColorHex && block.BorderColorHex !== 'Transparent'
                  ? `${block.ShapeBorderThickness}px solid ${block.BorderColorHex}`
                  : (block.Id === 'rect' ? `1px solid ${block.ColorHex}` : 'none'),
                borderRadius: block.Id === 'circle' ? '50%' : `${block.BorderRadius || 0}px`,
                backgroundColor: (block.Id === 'rect' || block.Id === 'circle' || block.Id === 'line') ? (block.ColorHex || 'transparent') : (block.BackgroundColorHex === 'Transparent' ? 'transparent' : (block.BackgroundColorHex || 'transparent')),
                textAlign: block.TextAlignment.toLowerCase() as any,
                zIndex: block.Id === 'image' ? 1 : (selectedBlockIndex === index ? 10 : 2)
              }} onMouseDown={e => handleMouseDown(e, index)} onDoubleClick={e => {
                if (block.Id === 'image') return;
                e.stopPropagation();
                setEditingBlockIndex(index);
                const isDesignItem = ['rect', 'circle', 'triangle', 'polygon', 'line', 'table', 'image', 'logo', 'custom_image'].includes(block.Id);
                if (!block.CustomText && !isDesignItem) {
                  updateBlock(index, { CustomText: `{${block.Id}}` }, true);
                }
              }}>
                {editingBlockIndex === index && block.Id !== 'table' && block.Id !== 'image' ? (
                  <textarea autoFocus className="canvas-editor" value={block.CustomText} onChange={e => updateBlock(index, { CustomText: e.target.value })} onBlur={() => setEditingBlockIndex(null)} onMouseDown={e => e.stopPropagation()} />
                ) : (
                  block.Id === 'table' ? (
                    <div className="table-preview" style={{
                      gridTemplateColumns: (block.TableColumnWidths || []).map(w => `${w}fr`).join(' '),
                      gridTemplateRows: (block.TableRowHeights || []).map(h => `${h}fr`).join(' ')
                    }}>
                      {selectedBlockIndex === index && (
                        <>
                          <button className="table-canvas-btn row-add" onClick={e => { e.stopPropagation(); addTableRow(index); }} title="Add Row"><Plus size={12} /></button>
                          <button className="table-canvas-btn col-add" onClick={e => { e.stopPropagation(); addTableCol(index); }} title="Add Column"><Plus size={12} /></button>
                        </>
                      )}
                      {getTableCells(block).map((cell, i) => (
                        <div key={i} className={`table-cell ${selectedCellPos?.row === cell.Row && selectedCellPos?.col === cell.Col ? 'cell-selected' : ''}`} style={{
                          borderColor: cell.BorderColor, background: cell.BackgroundColor,
                          borderStyle: cell.BorderStyle.toLowerCase() as any, borderWidth: `${cell.BorderT}px ${cell.BorderR}px ${cell.BorderB}px ${cell.BorderL}px`,
                          textAlign: cell.TextAlignment.toLowerCase() as any, fontWeight: cell.IsBold ? 'bold' : 'normal',
                          position: 'relative'
                        }} onMouseDown={e => { setSelectedCellPos({ row: cell.Row, col: cell.Col }); handleMouseDown(e, index, true); }} onDoubleClick={e => { e.stopPropagation(); setEditingCellPos({ row: cell.Row, col: cell.Col }); setEditingBlockIndex(index); }} onDragOver={e => e.preventDefault()} onDrop={e => {
                          e.stopPropagation();
                          const id = e.dataTransfer.getData('blockId');
                          if (id) updateTableCell(index, cell.Row, cell.Col, { Text: (cell.Text ? cell.Text + ' ' : '') + id });
                        }}>
                          {editingBlockIndex === index && editingCellPos?.row === cell.Row && editingCellPos?.col === cell.Col ? (
                            <textarea
                              autoFocus
                              className="cell-editor-canvas"
                              value={cell.Text}
                              onChange={e => updateTableCell(index, cell.Row, cell.Col, { Text: e.target.value })}
                              onBlur={() => { setEditingBlockIndex(null); setEditingCellPos(null); }}
                              onMouseDown={e => e.stopPropagation()}
                            />
                          ) : (
                            <>
                              {getDisplayText(cell.Text)}
                              <div className="col-resizer" onMouseDown={e => { e.stopPropagation(); setResizing({ blockIndex: index, type: 'col', index: cell.Col, startPos: e.clientX, startSize: (block.TableColumnWidths || [])[cell.Col], initialTotalSize: block.Width }); }} />
                              <div className="row-resizer" onMouseDown={e => { e.stopPropagation(); setResizing({ blockIndex: index, type: 'row', index: cell.Row, startPos: e.clientY, startSize: (block.TableRowHeights || [])[cell.Row], initialTotalSize: block.Height }); }} />
                            </>
                          )}
                        </div>
                      ))}
                    </div>
                  ) : isImg ? (
                    block.ImagePath ? (
                      <img src={block.ImagePath} style={{ width: '100%', height: '100%', objectFit: 'contain', pointerEvents: 'none' }} alt="Designer Block" draggable={false} />
                    ) : (
                      <div style={{ width: '100%', height: '100%', border: '1px dashed rgba(128,128,128,0.5)', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 6, color: '#888', background: 'rgba(0,0,0,0.05)' }}>
                        <ImageIcon size={20} />
                        <span style={{ fontSize: 10 }}>Empty Image</span>
                      </div>
                    )
                  ) : isShape ? (
                    <svg width="100%" height="100%" viewBox="0 0 100 100" preserveAspectRatio="none" style={{ pointerEvents: 'none', overflow: 'visible' }}>
                      <polygon
                        points={getPolygonPoints(block.Id === 'triangle' ? 3 : (block.PolygonSides || 5))}
                        fill={block.ColorHex === 'Transparent' ? 'none' : (block.ColorHex || '#CCCCCC')}
                        stroke={block.BorderColorHex === 'Transparent' ? 'none' : (block.BorderColorHex || 'transparent')}
                        strokeWidth={block.ShapeBorderThickness || 0}
                        vectorEffect="non-scaling-stroke"
                      />
                    </svg>
                  ) : block.Id === 'line' ? (
                    <div style={{ width: '100%', height: block.Height, backgroundColor: block.ColorHex }} />
                  ) : getDisplayText(block.CustomText, block.Id)
                )}
                {selectedBlockIndex === index && block.Id !== 'image' && (
                  <div
                    className="block-resizer-handle"
                    style={{
                      position: 'absolute',
                      right: -5,
                      bottom: -5,
                      width: 10,
                      height: 10,
                      backgroundColor: 'var(--primary)',
                      border: '2px solid white',
                      borderRadius: '50%',
                      cursor: 'se-resize',
                      zIndex: 100,
                      boxShadow: '0 2px 4px rgba(0,0,0,0.2)'
                    }}
                    onMouseDown={e => {
                      e.stopPropagation();
                      e.preventDefault();
                      setBlockResizing({
                        index,
                        startX: e.clientX,
                        startY: e.clientY,
                        startWidth: block.Width,
                        startHeight: block.Height
                      });
                    }}
                  />
                )}
              </div>
            );
          })}
        </div>
        <div className="ai-assistant" onMouseDown={e => e.stopPropagation()}>
          <div className="ai-icon-wrapper">
            <Sparkles size={18} className={isAiGenerating ? 'animate-pulse' : ''} />
          </div>
          <input className="ai-input" placeholder={isAiGenerating ? "Architecting..." : "AI Template Prompt..."} value={aiPrompt} onChange={e => setAiPrompt(e.target.value)} onKeyPress={e => e.key === 'Enter' && handleAiGenerate()} disabled={isAiGenerating} />
          <button className="ai-btn-stylish" onClick={handleAiGenerate} disabled={isAiGenerating}>
            {isAiGenerating ? <div className="loader-small" /> : <Send size={18} />}
          </button>
        </div>
      </main>

      {/* Properties */}
      <aside className="properties-panel" onMouseDown={e => e.stopPropagation()}>
        {selectedBlock ? (
          <div className="animate-in" style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            {/* Block Info & Delete */}
            <div className="prop-group">
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span className="caption" style={{ fontWeight: 'bold', color: 'var(--primary)' }}>Element: {selectedBlock.Id}</span>
                <button onClick={() => {
                  const newBlocks = blocks.filter((_, i) => i !== selectedBlockIndex!);
                  setBlocks(newBlocks);
                  updateHistory(newBlocks);
                  setSelectedBlockIndex(null);
                }} style={{ color: '#ff4444', border: 'none', background: 'none', cursor: 'pointer' }}>
                  <Trash2 size={16} />
                </button>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, marginTop: 8 }}>
                <div>
                  <label className="caption" style={{ fontSize: 10 }}>X Pos</label>
                  <input type="number" className="input-field" value={selectedBlock.X} onChange={e => updateBlock(selectedBlockIndex!, { X: parseInt(e.target.value) || 0 })} />
                </div>
                <div>
                  <label className="caption" style={{ fontSize: 10 }}>Y Pos</label>
                  <input type="number" className="input-field" value={selectedBlock.Y} onChange={e => updateBlock(selectedBlockIndex!, { Y: parseInt(e.target.value) || 0 })} />
                </div>
                <div>
                  <label className="caption" style={{ fontSize: 10 }}>Width</label>
                  <input type="number" className="input-field" value={selectedBlock.Width} onChange={e => updateBlock(selectedBlockIndex!, { Width: parseInt(e.target.value) || 10 })} />
                </div>
                <div>
                  <label className="caption" style={{ fontSize: 10 }}>Height</label>
                  <input type="number" className="input-field" value={selectedBlock.Height} onChange={e => updateBlock(selectedBlockIndex!, { Height: parseInt(e.target.value) || 10 })} />
                </div>
              </div>
            </div>

            {/* Custom Text / Placeholders content editor */}
            {selectedBlock.Id !== 'table' && selectedBlock.Id !== 'rect' && selectedBlock.Id !== 'circle' && selectedBlock.Id !== 'triangle' && selectedBlock.Id !== 'polygon' && selectedBlock.Id !== 'line' && !isImg && (
              <div className="prop-group">
                <span className="caption">Text Content</span>
                <textarea
                  className="input-field"
                  style={{ minHeight: 60, marginTop: 4, width: '100%', resize: 'vertical' }}
                  value={selectedBlock.CustomText}
                  onChange={e => updateBlock(selectedBlockIndex!, { CustomText: e.target.value })}
                  placeholder="Enter text or placeholder..."
                />
              </div>
            )}

            {/* Re-upload image option */}
            {isImg && (
              <div className="prop-group">
                <span className="caption">Image Source</span>
                <button className="btn btn-secondary" style={{ width: '100%', marginTop: 6 }} onClick={() => {
                  const input = document.createElement('input');
                  input.type = 'file';
                  input.accept = 'image/*';
                  input.onchange = (e: any) => {
                    const file = e.target.files?.[0];
                    if (file) {
                      const reader = new FileReader();
                      reader.onload = () => {
                        updateBlock(selectedBlockIndex!, { ImagePath: reader.result as string });
                      };
                      reader.readAsDataURL(file);
                    }
                  };
                  input.click();
                }}>
                  Change Image file
                </button>
              </div>
            )}
                 {/* Typography & Font Manager */}
            {selectedBlock.Id !== 'table' && selectedBlock.Id !== 'rect' && selectedBlock.Id !== 'circle' && selectedBlock.Id !== 'triangle' && selectedBlock.Id !== 'polygon' && selectedBlock.Id !== 'line' && (
              <div className="prop-group">
                <span className="caption" style={{ color: 'var(--primary)', fontWeight: 'bold' }}>Typography & Font Manager</span>
                
                {/* Font Family Selector */}
                <div style={{ marginBottom: 10 }}>
                  <label className="caption" style={{ fontSize: 9, marginBottom: 2 }}>Font Family</label>
                  <select className="input-field" style={{ width: '100%', marginBottom: 0 }} value={selectedBlock.FontFamily} onChange={e => updateBlock(selectedBlockIndex!, { FontFamily: e.target.value })}>
                    {FONT_FAMILIES.map(f => <option key={f} value={f}>{f}</option>)}
                  </select>
                </div>

                {/* Font Styles & Alignments Row */}
                <div style={{ display: 'flex', gap: 4, marginBottom: 12 }}>
                  <button className={`btn-small ${selectedBlock.IsBold ? 'active' : ''}`} title="Bold" onClick={() => updateBlock(selectedBlockIndex!, { IsBold: !selectedBlock.IsBold })}><Bold size={14} /></button>
                  <button className={`btn-small ${selectedBlock.IsItalic ? 'active' : ''}`} title="Italic" onClick={() => updateBlock(selectedBlockIndex!, { IsItalic: !selectedBlock.IsItalic })}><Italic size={14} /></button>
                  <button className={`btn-small ${selectedBlock.IsUnderlined ? 'active' : ''}`} title="Underline" onClick={() => updateBlock(selectedBlockIndex!, { IsUnderlined: !selectedBlock.IsUnderlined })}><Underline size={14} /></button>
                  <div style={{ flex: 1, borderRight: '1px solid var(--outline)', margin: '0 4px' }} />
                  <button className={`btn-small ${selectedBlock.TextAlignment === 'Left' ? 'active' : ''}`} title="Align Left" onClick={() => updateBlock(selectedBlockIndex!, { TextAlignment: 'Left' })}><AlignLeft size={14} /></button>
                  <button className={`btn-small ${selectedBlock.TextAlignment === 'Center' ? 'active' : ''}`} title="Align Center" onClick={() => updateBlock(selectedBlockIndex!, { TextAlignment: 'Center' })}><AlignCenter size={14} /></button>
                  <button className={`btn-small ${selectedBlock.TextAlignment === 'Right' ? 'active' : ''}`} title="Align Right" onClick={() => updateBlock(selectedBlockIndex!, { TextAlignment: 'Right' })}><AlignRight size={14} /></button>
                </div>

                {/* Font Size Range Slider & Adjustment Buttons */}
                <div style={{ marginBottom: 12 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 2 }}>
                    <label className="caption" style={{ fontSize: 9 }}>Font Size</label>
                    <span className="caption" style={{ fontSize: 9, fontWeight: 'bold' }}>{selectedBlock.FontSize}px</span>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <button className="btn-small" style={{ width: 28, height: 28, padding: 0 }} title="Decrease Size" onClick={() => updateBlock(selectedBlockIndex!, { FontSize: Math.max(6, selectedBlock.FontSize - 1) })}>-</button>
                    <input type="range" min="6" max="72" step="1" style={{ flex: 1, height: 4, cursor: 'pointer' }} value={selectedBlock.FontSize} onChange={e => updateBlock(selectedBlockIndex!, { FontSize: parseInt(e.target.value) || 12 })} />
                    <button className="btn-small" style={{ width: 28, height: 28, padding: 0 }} title="Increase Size" onClick={() => updateBlock(selectedBlockIndex!, { FontSize: Math.min(120, selectedBlock.FontSize + 1) })}>+</button>
                  </div>
                </div>

                {/* Numeric Size Value & Color Picker & Block Fill */}
                <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                  <div style={{ flex: 1 }}>
                    <label className="caption" style={{ fontSize: 9 }}>Size Value</label>
                    <input type="number" className="input-field" style={{ marginBottom: 0 }} value={selectedBlock.FontSize} onChange={e => updateBlock(selectedBlockIndex!, { FontSize: parseInt(e.target.value) || 12 })} />
                  </div>
                  <div>
                    <label className="caption" style={{ fontSize: 9 }}>Text Color</label>
                    <input type="color" className="input-field" style={{ width: 42, height: 32, padding: 0, border: '1px solid rgba(255,255,255,0.1)', cursor: 'pointer', marginBottom: 0 }} value={selectedBlock.ColorHex.startsWith('#') ? selectedBlock.ColorHex : '#000000'} onChange={e => updateBlock(selectedBlockIndex!, { ColorHex: e.target.value })} />
                  </div>
                  <div>
                    <label className="caption" style={{ fontSize: 9 }}>Block Fill</label>
                    <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
                      <input type="color" className="input-field" style={{ width: 32, height: 32, padding: 0, border: '1px solid rgba(255,255,255,0.1)', cursor: 'pointer', marginBottom: 0 }} value={selectedBlock.BackgroundColorHex && selectedBlock.BackgroundColorHex !== 'Transparent' ? selectedBlock.BackgroundColorHex : '#ffffff'} onChange={e => updateBlock(selectedBlockIndex!, { BackgroundColorHex: e.target.value })} disabled={selectedBlock.BackgroundColorHex === 'Transparent'} />
                      <button className={`btn-small ${selectedBlock.BackgroundColorHex === 'Transparent' ? 'active' : ''}`} style={{ height: 32, padding: '4px 8px', fontSize: 10 }} title="Make background transparent" onClick={() => updateBlock(selectedBlockIndex!, { BackgroundColorHex: selectedBlock.BackgroundColorHex === 'Transparent' ? '#ffffff' : 'Transparent' })}>
                        Clear
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* Shape Customizations (Colors, Borders) */}
            {(selectedBlock.Id === 'rect' || selectedBlock.Id === 'circle' || selectedBlock.Id === 'triangle' || selectedBlock.Id === 'polygon' || selectedBlock.Id === 'line') && (
              <div className="prop-group">
                <span className="caption">Shape Style</span>
                <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
                  <div style={{ flex: 1 }}>
                    <label className="caption" style={{ fontSize: 9 }}>Background Color</label>
                    <input type="color" className="input-field" style={{ width: '100%', height: 36, padding: 0, cursor: 'pointer' }} value={selectedBlock.ColorHex.startsWith('#') ? selectedBlock.ColorHex : '#cbd5e1'} onChange={e => updateBlock(selectedBlockIndex!, { ColorHex: e.target.value })} />
                  </div>
                  {selectedBlock.Id !== 'line' && (
                    <div style={{ flex: 1 }}>
                      <label className="caption" style={{ fontSize: 9 }}>Border Color</label>
                      <input type="color" className="input-field" style={{ width: '100%', height: 36, padding: 0, cursor: 'pointer' }} value={selectedBlock.BorderColorHex?.startsWith('#') ? selectedBlock.BorderColorHex : '#000000'} onChange={e => updateBlock(selectedBlockIndex!, { BorderColorHex: e.target.value })} />
                    </div>
                  )}
                </div>
                {selectedBlock.Id !== 'line' && (
                  <div style={{ marginTop: 8 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                      <label className="caption" style={{ fontSize: 9 }}>Border Thickness</label>
                      <span className="caption" style={{ fontSize: 9, fontWeight: 'bold' }}>{selectedBlock.ShapeBorderThickness || 0}px</span>
                    </div>
                    <input type="range" min="0" max="15" step="1" style={{ width: '100%' }} value={selectedBlock.ShapeBorderThickness || 0} onChange={e => updateBlock(selectedBlockIndex!, { ShapeBorderThickness: parseInt(e.target.value) })} />
                  </div>
                )}
              </div>
            )}

            {/* Corner Radius & Polygon Sides & Opacity Slider */}
            <div className="prop-group">
              <span className="caption">Advanced Features</span>

              {/* Corner Radius for rect shapes / text boxes */}
              {selectedBlock.Id !== 'circle' && selectedBlock.Id !== 'table' && selectedBlock.Id !== 'line' && (
                <div style={{ marginTop: 6 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <label className="caption" style={{ fontSize: 9 }}>Corner Radius</label>
                    <span className="caption" style={{ fontSize: 9, fontWeight: 'bold' }}>{selectedBlock.BorderRadius || 0}px</span>
                  </div>
                  <input type="range" min="0" max="60" step="1" style={{ width: '100%' }} value={selectedBlock.BorderRadius || 0} onChange={e => updateBlock(selectedBlockIndex!, { BorderRadius: parseInt(e.target.value) })} />
                </div>
              )}

              {/* Polygon Sides Slider */}
              {selectedBlock.Id === 'polygon' && (
                <div style={{ marginTop: 6 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <label className="caption" style={{ fontSize: 9 }}>Number of Edges</label>
                    <span className="caption" style={{ fontSize: 9, fontWeight: 'bold' }}>{selectedBlock.PolygonSides || 5}</span>
                  </div>
                  <input type="range" min="3" max="12" step="1" style={{ width: '100%' }} value={selectedBlock.PolygonSides || 5} onChange={e => updateBlock(selectedBlockIndex!, { PolygonSides: parseInt(e.target.value) })} />
                </div>
              )}

              {/* Opacity Slider */}
              <div style={{ marginTop: 6 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                  <label className="caption" style={{ fontSize: 9 }}>Transparency (Opacity)</label>
                  <span className="caption" style={{ fontSize: 9, fontWeight: 'bold' }}>{Math.round((selectedBlock.Opacity || 1) * 100)}%</span>
                </div>
                <input type="range" min="0.0" max="1.0" step="0.05" style={{ width: '100%' }} value={selectedBlock.Opacity || 1} onChange={e => updateBlock(selectedBlockIndex!, { Opacity: parseFloat(e.target.value) })} />
              </div>
            </div>

            {/* Visibility Condition dropdown */}
            <div className="prop-group">
              <span className="caption">Visibility Logic</span>
              <select className="input-field" style={{ width: '100%', marginTop: 4 }} value={selectedBlock.VisibilityCondition || ''} onChange={e => updateBlock(selectedBlockIndex!, { VisibilityCondition: e.target.value })}>
                <option value="">Always Visible</option>
                <option value="DiagnosticsNotEmpty">Visible only when Diagnostics is not empty</option>
                <option value="CostNotEmpty">Visible only when Estimated Cost is not empty</option>
                <option value="AccessoriesNotEmpty">Visible only when Accessories are listed</option>
                <option value="Phone2NotEmpty">Visible only when Alt Contact Phone 2 is listed</option>
                <option value="SerialNumberNotEmpty">Visible only when Serial Number is not empty</option>
              </select>
            </div>

            {/* Table wide styling options */}
            {selectedBlock.Id === 'table' && (
              <div className="prop-group">
                <span className="caption" style={{ color: 'var(--primary)', fontWeight: 'bold' }}>Table Configuration</span>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, marginTop: 8 }}>
                  <div>
                    <label className="caption" style={{ fontSize: 9 }}>Grid Bg</label>
                    <input type="color" className="input-field" style={{ width: '100%', height: 32, cursor: 'pointer' }} value={selectedBlock.TableBackgroundColorHex?.startsWith('#') ? selectedBlock.TableBackgroundColorHex : '#ffffff'} onChange={e => updateBlock(selectedBlockIndex!, { TableBackgroundColorHex: e.target.value })} />
                  </div>
                  <div>
                    <label className="caption" style={{ fontSize: 9 }}>Grid Borders</label>
                    <input type="color" className="input-field" style={{ width: '100%', height: 32, cursor: 'pointer' }} value={selectedBlock.BorderColorHex?.startsWith('#') ? selectedBlock.BorderColorHex : '#CCCCCC'} onChange={e => updateBlock(selectedBlockIndex!, { BorderColorHex: e.target.value })} />
                  </div>
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, marginTop: 8 }}>
                  <button className="btn-small" style={{ display: 'flex', alignItems: 'center', gap: 4, justifyContent: 'center' }} onClick={() => addTableRow(selectedBlockIndex!)}><Plus size={12} /> Row</button>
                  <button className="btn-small" style={{ display: 'flex', alignItems: 'center', gap: 4, justifyContent: 'center', color: '#ff4444' }} onClick={() => removeTableRow(selectedBlockIndex!)}><Trash2 size={12} /> Row</button>
                  <button className="btn-small" style={{ display: 'flex', alignItems: 'center', gap: 4, justifyContent: 'center' }} onClick={() => addTableCol(selectedBlockIndex!)}><Plus size={12} /> Column</button>
                  <button className="btn-small" style={{ display: 'flex', alignItems: 'center', gap: 4, justifyContent: 'center', color: '#ff4444' }} onClick={() => removeTableCol(selectedBlockIndex!)}><Trash2 size={12} /> Column</button>
                </div>
              </div>
            )}

            {/* Individual Cell formatting properties */}
            {selectedCell && (
              <div className="prop-group cell-editor-box" style={{ background: 'rgba(255,255,255,0.03)', border: '1px solid rgba(255,255,255,0.05)', padding: 12, borderRadius: 8 }}>
                <span className="caption" style={{ color: 'var(--primary)', fontWeight: 'bold' }}>Cell [{selectedCell.Row + 1}, {selectedCell.Col + 1}] Options</span>
                <textarea
                  className="input-field"
                  style={{ width: '100%', minHeight: 40, resize: 'vertical', marginTop: 4, fontSize: 12 }}
                  value={selectedCell.Text}
                  onChange={e => updateTableCell(selectedBlockIndex!, selectedCell.Row, selectedCell.Col, { Text: e.target.value })}
                  placeholder="Cell text or preset field..."
                />
                
                {/* Cell backgrounds and borders */}
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6, marginTop: 8 }}>
                  <div>
                    <label className="caption" style={{ fontSize: 9 }}>Cell Fill</label>
                    <input type="color" className="input-field" style={{ width: '100%', height: 28, cursor: 'pointer' }} value={selectedCell.BackgroundColor?.startsWith('#') ? selectedCell.BackgroundColor : '#ffffff'} onChange={e => updateTableCell(selectedBlockIndex!, selectedCell.Row, selectedCell.Col, { BackgroundColor: e.target.value })} />
                  </div>
                  <div>
                    <label className="caption" style={{ fontSize: 9 }}>Cell Border</label>
                    <input type="color" className="input-field" style={{ width: '100%', height: 28, cursor: 'pointer' }} value={selectedCell.BorderColor?.startsWith('#') ? selectedCell.BorderColor : '#CCCCCC'} onChange={e => updateTableCell(selectedBlockIndex!, selectedCell.Row, selectedCell.Col, { BorderColor: e.target.value })} />
                  </div>
                </div>

                {/* Cell Typography styles */}
                <div style={{ display: 'flex', gap: 4, marginTop: 8, alignItems: 'center' }}>
                  <button className={`btn-small ${selectedCell.IsBold ? 'active' : ''}`} style={{ padding: '4px 8px' }} onClick={() => updateTableCell(selectedBlockIndex!, selectedCell.Row, selectedCell.Col, { IsBold: !selectedCell.IsBold })}><Bold size={12} /></button>
                  <button className={`btn-small ${selectedCell.IsItalic ? 'active' : ''}`} style={{ padding: '4px 8px' }} onClick={() => updateTableCell(selectedBlockIndex!, selectedCell.Row, selectedCell.Col, { IsItalic: !selectedCell.IsItalic })}><Italic size={12} /></button>
                  <div style={{ flex: 1 }} />
                  <button className={`btn-small ${selectedCell.TextAlignment === 'Left' ? 'active' : ''}`} style={{ padding: '4px 8px' }} onClick={() => updateTableCell(selectedBlockIndex!, selectedCell.Row, selectedCell.Col, { TextAlignment: 'Left' })}><AlignLeft size={12} /></button>
                  <button className={`btn-small ${selectedCell.TextAlignment === 'Center' ? 'active' : ''}`} style={{ padding: '4px 8px' }} onClick={() => updateTableCell(selectedBlockIndex!, selectedCell.Row, selectedCell.Col, { TextAlignment: 'Center' })}><AlignCenter size={12} /></button>
                  <button className={`btn-small ${selectedCell.TextAlignment === 'Right' ? 'active' : ''}`} style={{ padding: '4px 8px' }} onClick={() => updateTableCell(selectedBlockIndex!, selectedCell.Row, selectedCell.Col, { TextAlignment: 'Right' })}><AlignRight size={12} /></button>
                </div>
              </div>
            )}
          </div>
        ) : (
          <div style={{ textAlign: 'center', opacity: 0.2, marginTop: 100 }}>
            <MousePointer2 size={64} style={{ margin: '0 auto' }} />
            <p style={{ marginTop: 8 }}>Select an element on canvas to configure its properties.</p>
          </div>
        )}
      </aside>
    </div>
  );
}

export default App;
