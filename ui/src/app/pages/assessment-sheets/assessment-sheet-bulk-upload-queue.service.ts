import { Injectable } from '@angular/core';
import { AssessmentSheetPdfKind } from './assessment-sheet-plan-preview.models';

export interface AssessmentSheetBulkUploadSummary {
  kind: AssessmentSheetPdfKind;
  successCount: number;
  failCount: number;
  total: number;
}

// Điều phối chuỗi điều hướng tự động qua từng màn preview khcn/KQ, đúng bằng cơ chế nút
// "Tạo PDF ... lên Google Drive" + `?auto=1` đã có (ASH-FB-W3 / G6a) — chỉ khác là sau khi
// xong 1 bảng thì tự điều hướng sang bảng kế tiếp trong hàng đợi thay vì quay lại màn edit.
// `AssessmentSheetPlanPreviewComponent` đọc `running`/gọi `next()`/`recordResult()` để biết
// đang chạy đơn lẻ (running=false, giữ hành vi G6a cũ) hay đang chạy theo hàng đợi bulk.
@Injectable({ providedIn: 'root' })
export class AssessmentSheetBulkUploadQueueService {
  private remainingIds: string[] = [];
  private kindValue: AssessmentSheetPdfKind = 'result';
  private successCount = 0;
  private failCount = 0;
  private total = 0;
  private runningFlag = false;

  get running(): boolean {
    return this.runningFlag;
  }

  get kind(): AssessmentSheetPdfKind {
    return this.kindValue;
  }

  // Khởi tạo hàng đợi mới, trả về id đầu tiên (đã lấy khỏi hàng đợi) hoặc null nếu rỗng.
  // `running` chỉ bật khi thực sự có id để chạy — danh sách rỗng không để lại trạng thái "đang chạy".
  start(kind: AssessmentSheetPdfKind, ids: readonly string[]): string | null {
    this.kindValue = kind;
    this.remainingIds = [...ids];
    this.successCount = 0;
    this.failCount = 0;
    this.total = ids.length;
    const first = this.next();
    this.runningFlag = first !== null;
    return first;
  }

  recordResult(success: boolean): void {
    if (success) {
      this.successCount += 1;
    } else {
      this.failCount += 1;
    }
  }

  // Lấy id kế tiếp trong hàng đợi; trả null khi đã hết (kết thúc hàng đợi — gọi finish()).
  next(): string | null {
    return this.remainingIds.shift() ?? null;
  }

  // Kết thúc hàng đợi bình thường (đã xử lý hết), trả tổng kết để hiện notify.
  finish(): AssessmentSheetBulkUploadSummary {
    const summary: AssessmentSheetBulkUploadSummary = {
      kind: this.kindValue,
      successCount: this.successCount,
      failCount: this.failCount,
      total: this.total
    };
    this.runningFlag = false;
    this.remainingIds = [];
    return summary;
  }

  // Hủy hàng đợi khi màn preview bị rời giữa chừng (người dùng bấm "Quay lại"/điều hướng đi
  // nơi khác) — tránh lần chạy đơn lẻ (?auto=1) sau đó hiểu nhầm là vẫn đang chạy bulk.
  abort(): void {
    this.remainingIds = [];
    this.runningFlag = false;
  }
}
