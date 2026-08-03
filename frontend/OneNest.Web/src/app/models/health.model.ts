export interface HealthResponse {
  status: string;
  application: string;
  version: string;
  timestamp: string;
}

// ---------- Enums ----------

export enum AppointmentStatus {
  Scheduled = 1,
  Completed = 2,
  Cancelled = 3
}

export const APPOINTMENT_STATUS_LABELS: Record<number, string> = {
  [AppointmentStatus.Scheduled]: 'Scheduled',
  [AppointmentStatus.Completed]: 'Completed',
  [AppointmentStatus.Cancelled]: 'Cancelled'
};

export enum MedicineFoodTiming {
  BeforeFood = 1,
  AfterFood = 2,
  Anytime = 3
}

export const FOOD_TIMING_LABELS: Record<number, string> = {
  [MedicineFoodTiming.BeforeFood]: 'Before Food',
  [MedicineFoodTiming.AfterFood]: 'After Food',
  [MedicineFoodTiming.Anytime]: 'Anytime'
};

export enum MedicalReportCategory {
  LabReport = 1,
  Prescription = 2,
  Scan = 3,
  DischargeSummary = 4,
  Other = 5
}

export const REPORT_CATEGORY_LABELS: Record<number, string> = {
  [MedicalReportCategory.LabReport]: 'Lab Report',
  [MedicalReportCategory.Prescription]: 'Prescription',
  [MedicalReportCategory.Scan]: 'Scan',
  [MedicalReportCategory.DischargeSummary]: 'Discharge Summary',
  [MedicalReportCategory.Other]: 'Other'
};

// ---------- Medicine ----------

export interface Medicine {
  id: string;
  name: string;
  dosage: string;
  frequency: string;
  morning: boolean;
  afternoon: boolean;
  night: boolean;
  startDate: string;
  endDate: string | null;
  instructions: string;
  foodTiming: MedicineFoodTiming;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface SaveMedicineRequest {
  name: string;
  dosage: string;
  frequency: string;
  morning: boolean;
  afternoon: boolean;
  night: boolean;
  startDate: string;
  endDate: string | null;
  instructions: string;
  foodTiming: MedicineFoodTiming;
  isActive: boolean;
}

// ---------- Appointment ----------

export interface Appointment {
  id: string;
  doctorName: string;
  hospital: string;
  specialty: string;
  date: string;
  time: string;
  notes: string;
  status: AppointmentStatus;
  createdAt: string;
  updatedAt: string | null;
}

export interface SaveAppointmentRequest {
  doctorName: string;
  hospital: string;
  specialty: string;
  date: string;
  time: string;
  notes: string;
  status: AppointmentStatus;
}

// ---------- Medical Record ----------

export interface MedicalRecord {
  id: string;
  bloodGroup: string;
  heightCm: number | null;
  weightKg: number | null;
  allergies: string;
  existingConditions: string;
  emergencyContactName: string;
  emergencyContactPhone: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface SaveMedicalRecordRequest {
  bloodGroup: string;
  heightCm: number | null;
  weightKg: number | null;
  allergies: string;
  existingConditions: string;
  emergencyContactName: string;
  emergencyContactPhone: string;
}

// ---------- Medical Report ----------

export interface MedicalReport {
  id: string;
  title: string;
  category: MedicalReportCategory;
  doctorName: string;
  hospital: string;
  reportDate: string;
  description: string;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface UpdateMedicalReportRequest {
  title: string;
  category: MedicalReportCategory;
  doctorName: string;
  hospital: string;
  reportDate: string;
  description: string;
}

// ---------- Health Summary ----------

export interface MedicineTimingDistribution {
  timing: string;
  count: number;
}

export interface AppointmentTimelinePoint {
  year: number;
  month: number;
  count: number;
}

export interface HealthSummary {
  activeMedicines: number;
  todayMedicines: number;
  expiringSoonMedicines: number;
  upcomingAppointments: number;
  pastAppointments: number;
  totalReports: number;
  lastRecordUpdate: string | null;
  medicineDistribution: MedicineTimingDistribution[];
  appointmentTimeline: AppointmentTimelinePoint[];
  recentReports: MedicalReport[];
  upcomingAppointmentsList: Appointment[];
}
