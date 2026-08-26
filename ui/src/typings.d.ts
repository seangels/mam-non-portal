declare module 'html2pdf.js' {
  export interface Html2PdfWorker {
    set(options: unknown): Html2PdfWorker;
    from(element: HTMLElement): Html2PdfWorker;
    outputPdf(type: 'bloburl'): Promise<string>;
    outputPdf(type: 'blob'): Promise<Blob>;
  }

  function html2pdf(): Html2PdfWorker;

  export default html2pdf;
}

interface Window {
  html2pdf?: () => import('html2pdf.js').Html2PdfWorker;
}
