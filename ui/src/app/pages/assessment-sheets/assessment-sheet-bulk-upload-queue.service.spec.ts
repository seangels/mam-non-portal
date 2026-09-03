import { AssessmentSheetBulkUploadQueueService } from './assessment-sheet-bulk-upload-queue.service';

describe('AssessmentSheetBulkUploadQueueService', () => {
  let service: AssessmentSheetBulkUploadQueueService;

  beforeEach(() => {
    service = new AssessmentSheetBulkUploadQueueService();
  });

  it('is idle until start() is called', () => {
    expect(service.running).toBeFalse();
  });

  it('dequeues ids in order and finishes with the right tally', () => {
    const first = service.start('result', ['a', 'b', 'c']);
    expect(first).toBe('a');
    expect(service.running).toBeTrue();
    expect(service.kind).toBe('result');

    service.recordResult(true);
    expect(service.next()).toBe('b');

    service.recordResult(false);
    expect(service.next()).toBe('c');

    service.recordResult(true);
    expect(service.next()).toBeNull();

    const summary = service.finish();
    expect(summary).toEqual({ kind: 'result', successCount: 2, failCount: 1, total: 3 });
    expect(service.running).toBeFalse();
  });

  it('returns null and stays idle when started with an empty list', () => {
    expect(service.start('result', [])).toBeNull();
    expect(service.running).toBeFalse();
  });

  it('abort() clears a queue left running mid-way (e.g. user navigated away)', () => {
    service.start('result', ['a', 'b']);
    expect(service.running).toBeTrue();

    service.abort();
    expect(service.running).toBeFalse();
    // Hàng đợi trống sau abort — next() không còn trả id cũ.
    expect(service.next()).toBeNull();
  });

  it('a fresh start() replaces any leftover state from a previous run', () => {
    service.start('result', ['a']);
    service.recordResult(false);
    // Không gọi finish()/next() hết hàng đợi — giả lập bỏ dở giữa chừng rồi chạy lại.
    const restarted = service.start('plan', ['x', 'y']);
    expect(restarted).toBe('x');
    expect(service.kind).toBe('plan');
    expect(service.next()).toBe('y');
    const summary = service.finish();
    expect(summary).toEqual({ kind: 'plan', successCount: 0, failCount: 0, total: 2 });
  });
});
