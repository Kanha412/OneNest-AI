import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  Medicine,
  SaveMedicineRequest,
  Appointment,
  SaveAppointmentRequest,
  AppointmentStatus,
  MedicalRecord,
  SaveMedicalRecordRequest,
  MedicalReport,
  UpdateMedicalReportRequest,
  MedicalReportCategory,
  HealthSummary
} from '../models/health.model';

@Injectable({
  providedIn: 'root'
})
export class HealthHubService {

  private readonly http = inject(HttpClient);

  private readonly medicinesUrl = `${environment.apiBaseUrl}/Medicines`;
  private readonly appointmentsUrl = `${environment.apiBaseUrl}/Appointments`;
  private readonly recordsUrl = `${environment.apiBaseUrl}/MedicalRecords`;
  private readonly reportsUrl = `${environment.apiBaseUrl}/MedicalReports`;
  private readonly summaryUrl = `${environment.apiBaseUrl}/health-hub/summary`;

  // ---------- Medicines ----------

  getMedicines(search?: string, isActive?: boolean | null): Observable<Medicine[]> {
    const params: Record<string, string> = {};
    if (search && search.trim()) {
      params['search'] = search.trim();
    }
    if (isActive !== null && isActive !== undefined) {
      params['isActive'] = String(isActive);
    }
    return this.http.get<Medicine[]>(this.medicinesUrl, { params });
  }

  createMedicine(payload: SaveMedicineRequest): Observable<Medicine> {
    return this.http.post<Medicine>(this.medicinesUrl, payload);
  }

  updateMedicine(id: string, payload: SaveMedicineRequest): Observable<Medicine> {
    return this.http.put<Medicine>(`${this.medicinesUrl}/${id}`, payload);
  }

  deleteMedicine(id: string) {
    return this.http.delete(`${this.medicinesUrl}/${id}`);
  }

  // ---------- Appointments ----------

  getAppointments(search?: string, status?: AppointmentStatus | null): Observable<Appointment[]> {
    const params: Record<string, string> = {};
    if (search && search.trim()) {
      params['search'] = search.trim();
    }
    if (status !== null && status !== undefined) {
      params['status'] = String(status);
    }
    return this.http.get<Appointment[]>(this.appointmentsUrl, { params });
  }

  createAppointment(payload: SaveAppointmentRequest): Observable<Appointment> {
    return this.http.post<Appointment>(this.appointmentsUrl, payload);
  }

  updateAppointment(id: string, payload: SaveAppointmentRequest): Observable<Appointment> {
    return this.http.put<Appointment>(`${this.appointmentsUrl}/${id}`, payload);
  }

  deleteAppointment(id: string) {
    return this.http.delete(`${this.appointmentsUrl}/${id}`);
  }

  // ---------- Medical Record ----------

  getMedicalRecord(): Observable<MedicalRecord> {
    return this.http.get<MedicalRecord>(this.recordsUrl);
  }

  saveMedicalRecord(payload: SaveMedicalRecordRequest): Observable<MedicalRecord> {
    return this.http.put<MedicalRecord>(this.recordsUrl, payload);
  }

  // ---------- Medical Reports ----------

  getReports(search?: string, category?: MedicalReportCategory | null): Observable<MedicalReport[]> {
    const params: Record<string, string> = {};
    if (search && search.trim()) {
      params['search'] = search.trim();
    }
    if (category !== null && category !== undefined) {
      params['category'] = String(category);
    }
    return this.http.get<MedicalReport[]>(this.reportsUrl, { params });
  }

  uploadReport(
    file: File,
    title: string,
    category: MedicalReportCategory,
    doctorName: string,
    hospital: string,
    reportDate: string,
    description: string
  ): Observable<MedicalReport> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('title', title);
    formData.append('category', String(category));
    formData.append('doctorName', doctorName ?? '');
    formData.append('hospital', hospital ?? '');
    formData.append('reportDate', reportDate);
    formData.append('description', description ?? '');
    return this.http.post<MedicalReport>(this.reportsUrl, formData);
  }

  updateReport(id: string, payload: UpdateMedicalReportRequest): Observable<MedicalReport> {
    return this.http.put<MedicalReport>(`${this.reportsUrl}/${id}`, payload);
  }

  deleteReport(id: string) {
    return this.http.delete(`${this.reportsUrl}/${id}`);
  }

  downloadReport(id: string): Observable<Blob> {
    return this.http.get(`${this.reportsUrl}/${id}/download`, {
      responseType: 'blob'
    });
  }

  previewReport(id: string): Observable<Blob> {
    return this.http.get(`${this.reportsUrl}/${id}/preview`, {
      responseType: 'blob'
    });
  }

  // ---------- Summary ----------

  getSummary(): Observable<HealthSummary> {
    return this.http.get<HealthSummary>(this.summaryUrl);
  }
}
