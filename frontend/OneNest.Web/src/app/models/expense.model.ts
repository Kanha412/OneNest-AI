export enum TransactionType {
  Income = 1,
  Expense = 2
}

export enum ExpenseCategory {
  Food = 1,
  Travel = 2,
  Shopping = 3,
  Bills = 4,
  Entertainment = 5,
  Health = 6,
  Education = 7,
  Salary = 8,
  Investment = 9,
  Other = 10
}

export const EXPENSE_CATEGORY_LABELS: Record<number, string> = {
  [ExpenseCategory.Food]: 'Food',
  [ExpenseCategory.Travel]: 'Travel',
  [ExpenseCategory.Shopping]: 'Shopping',
  [ExpenseCategory.Bills]: 'Bills',
  [ExpenseCategory.Entertainment]: 'Entertainment',
  [ExpenseCategory.Health]: 'Health',
  [ExpenseCategory.Education]: 'Education',
  [ExpenseCategory.Salary]: 'Salary',
  [ExpenseCategory.Investment]: 'Investment',
  [ExpenseCategory.Other]: 'Other'
};

export interface Expense {
  id: string;
  title: string;
  amount: number;
  category: ExpenseCategory;
  transactionType: TransactionType;
  date: string;
  notes: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface CategoryExpense {
  category: ExpenseCategory;
  totalAmount: number;
}

export interface MonthlyExpense {
  year: number;
  month: number;
  income: number;
  expense: number;
}

export interface ExpenseSummary {
  totalIncome: number;
  totalExpense: number;
  currentBalance: number;
  thisMonthIncome: number;
  thisMonthExpense: number;
  topExpenseCategory: ExpenseCategory | null;
  recentTransactions: Expense[];
  categoryBreakdown: CategoryExpense[];
  monthlyBreakdown: MonthlyExpense[];
}
