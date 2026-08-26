import { AfterViewChecked, Component, ElementRef, HostListener, OnInit, ViewChild } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import html2pdf from 'html2pdf.js';
import notify from 'devextreme/ui/notify';
import { ApiError } from '../../core/models/api-error';
import { AssessmentSheetDetail } from '../../core/models/api.models.assessment-sheets';
import { AssessmentSheetsService } from '../../core/services/assessment-sheets.service';
import {
  AssessmentSheetPlanPreviewModel,
  buildAssessmentSheetPlanPreview,
  planGradeText,
  planGradeBgColor,
  planGradeColor,
  planNoteText,
} from './assessment-sheet-plan-preview.models';

@Component({
  selector: 'app-assessment-sheet-plan-preview',
  templateUrl: './assessment-sheet-plan-preview.component.html',
  styleUrls: ['./assessment-sheet-plan-preview.component.scss']
})
export class AssessmentSheetPlanPreviewComponent implements OnInit, AfterViewChecked {
  @ViewChild('pdfPage') pdfPage?: ElementRef<HTMLElement>;
  @ViewChild('pdfContent') pdfContent?: ElementRef<HTMLElement>;

  sheetId = '';
  sheet: AssessmentSheetDetail | null = null;
  model: AssessmentSheetPlanPreviewModel | null = null;
  loading = false;
  generatingPdf = false;
  uploadingPdf = false;
  loadError = '';
  actionError = '';
  driveFileLink = '';

  // Tracks which model reference has already been fitted to the page so
  // fitContentToPage() only re-measures once per model change, not on every
  // change-detection pass through ngAfterViewChecked.
  private lastFittedModel: AssessmentSheetPlanPreviewModel | null = null;

  readonly pdfOptions: Record<string, unknown> = {
    "margin": -5,
    filename: 'ke-hoach-ca-nhan.pdf',
    image: { type: 'jpeg', quality: 0.98 },
    html2canvas: {
      scale: 2,
      // letterRendering: true,
      useCORS: true,
      backgroundColor: '#ffffff'
    },
    jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' },
    pagebreak: { mode: ['css', 'legacy'], avoid: ['tr', '.no-break'] }
  };

  constructor(
    private readonly assessmentSheets: AssessmentSheetsService,
    private readonly route: ActivatedRoute,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.sheetId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.sheetId) {
      this.loadError = 'Không tìm thấy mã bảng đánh giá trong đường dẫn.';
      return;
    }
    void this.load();
  }

  ngAfterViewChecked(): void {
    // *ngFor row count is dynamic per sheet, so only re-fit once the view has
    // settled for a genuinely new model reference (avoids re-fitting on
    // every change-detection pass).
    if (this.model && this.model !== this.lastFittedModel) {
      this.fitContentToPage();
      this.lastFittedModel = this.model;
    }
  }

  @HostListener('window:resize')
  onWindowResize(): void {
    this.fitContentToPage();
  }

  async load(): Promise<void> {
    this.loading = true;
    this.loadError = '';
    this.actionError = '';
    try {
      const sheet = await firstValueFrom(this.assessmentSheets.get(this.sheetId));
      this.applySheet(sheet);
    } catch (error) {
      this.loadError = this.withTrace(ApiError.from(error));
    } finally {
      this.loading = false;
    }
  }

  goBack(): void {
    void this.router.navigate(['/assessment-sheets', this.sheetId, 'edit']);
  }

  async openPdf(): Promise<void> {
    if (this.generatingPdf || this.uploadingPdf) {
      return;
    }
    // window.print();
    // return;
    const worker = this.createPdfWorker(true);
    if (!worker) {
      return;
    }

    this.generatingPdf = true;
    this.actionError = '';
    try {
      const blobUrl = await worker.outputPdf('bloburl');
      window.open(blobUrl);
    } catch {
      this.actionError = 'Không thể tạo PDF kế hoạch. Vui lòng thử lại.';
    } finally {
      this.generatingPdf = false;
    }
  }

  async uploadPdfToDrive(): Promise<void> {
    if (this.generatingPdf || this.uploadingPdf || !this.model) {
      return;
    }

    const worker = this.createPdfWorker(false);
    if (!worker) {
      this.actionError = 'Không thể tạo PDF kế hoạch vì thư viện html2pdf chưa sẵn sàng.';
      return;
    }

    this.uploadingPdf = true;
    this.actionError = '';
    try {
      const blob = await worker.outputPdf('blob');
      const saved = await firstValueFrom(this.assessmentSheets.uploadPlanPdf(
        this.sheetId,
        blob,
        this.model.fileName
      ));
      this.applySheet(saved);
      this.driveFileLink = saved.planFileLinkPdf ?? '';
      notify('Đã tạo PDF kế hoạch lên Google Drive.', 'success', 2500);
    } catch (error) {
      const apiError = ApiError.from(error);
      this.actionError = this.withTrace(apiError);
    } finally {
      this.uploadingPdf = false;
    }
  }

  gradeText = planGradeText;
  gradeColor = planGradeColor;
  gradeBgColor = planGradeBgColor;
  noteText = planNoteText;

  private applySheet(sheet: AssessmentSheetDetail): void {
    this.sheet = sheet;
    this.model = buildAssessmentSheetPlanPreview(sheet);
    this.pdfOptions['filename'] = this.model.fileName;
    console.log(this.pdfOptions['filename'])
    this.driveFileLink = sheet.planFileLinkPdf ?? '';
  }

  // Ports the auto-fit-to-one-page mechanism from docs/samples/khcn-standalone.html:
  // .pdf-page has a fixed height and overflow:hidden, so it always measures as
  // exactly one A4 page for html2canvas/html2pdf. .pdf-content is scaled down
  // (never up) only when its natural content height overflows the available
  // page area, keeping everything on a single page regardless of row count.
  private fitContentToPage(): void {
    const page = this.pdfPage?.nativeElement;
    const content = this.pdfContent?.nativeElement;
    if (!page || !content) {
      return;
    }

    content.style.transform = 'none';

    const pageStyle = window.getComputedStyle(page);
    const paddingTop = parseFloat(pageStyle.paddingTop) || 0;
    const paddingBottom = parseFloat(pageStyle.paddingBottom) || 0;
    const availableHeight = page.clientHeight - paddingTop - paddingBottom;
    const naturalHeight = content.scrollHeight;

    if (availableHeight > 0 && naturalHeight > availableHeight) {
      // 0.5% safety margin against sub-pixel rounding differences when html2canvas re-renders.
      const scale = (availableHeight / naturalHeight) * 0.995;
      content.style.transform = `scale(${scale})`;
    }
  }

  private createPdfWorker(allowPrintFallback: boolean): import('html2pdf.js').Html2PdfWorker | null {
    // Always re-measure against current content so exported PDFs reflect the
    // on-screen preview, even if the view-checked fit hasn't run yet (e.g.
    // called immediately after a model reload).
    this.fitContentToPage();

    const page = this.pdfPage?.nativeElement;
    if (!page) {
      this.actionError = 'Không tìm thấy nội dung preview để tạo PDF.';
      return null;
    }

    const html2pdfFactory = window.html2pdf ?? html2pdf;
    if (!html2pdfFactory) {
      if (allowPrintFallback) {
        window.print();
      }
      return null;
    }

    return html2pdfFactory().set(this.pdfOptions).from(page);
  }

  private withTrace(error: ApiError): string {
    return error.traceId ? `${error.message} Mã tra cứu: ${error.traceId}` : error.message;
  }
}
