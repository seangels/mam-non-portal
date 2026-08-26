import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
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
  planNoteText
} from './assessment-sheet-plan-preview.models';

@Component({
  selector: 'app-assessment-sheet-plan-preview',
  templateUrl: './assessment-sheet-plan-preview.component.html',
  styleUrls: ['./assessment-sheet-plan-preview.component.scss']
})
export class AssessmentSheetPlanPreviewComponent implements OnInit {
  @ViewChild('pdfPage') pdfPage?: ElementRef<HTMLElement>;

  sheetId = '';
  sheet: AssessmentSheetDetail | null = null;
  model: AssessmentSheetPlanPreviewModel | null = null;
  loading = false;
  generatingPdf = false;
  uploadingPdf = false;
  loadError = '';
  actionError = '';
  driveFileLink = '';

  readonly pdfOptions: Record<string, unknown> = {
    margin: 0,
    filename: 'ke-hoach-ca-nhan.pdf',
    image: { type: 'jpeg', quality: 0.98 },
    html2canvas: {
      scale: 2,
      letterRendering: true,
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
  noteText = planNoteText;

  private applySheet(sheet: AssessmentSheetDetail): void {
    this.sheet = sheet;
    this.model = buildAssessmentSheetPlanPreview(sheet);
    this.pdfOptions['filename'] = this.model.fileName;
    this.driveFileLink = sheet.planFileLinkPdf ?? '';
  }

  private createPdfWorker(allowPrintFallback: boolean): import('html2pdf.js').Html2PdfWorker | null {
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
