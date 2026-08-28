/**
 * Permission codes as the API defines them (KH.Domain.Constants.Policies and FsmsPolicies).
 * Route guards, nav items and `*ngxPermissionsOnly` all key off these rather than off role names:
 * a role's grants are editable from the admin screen, so gating on the permission keeps the UI
 * honest when someone changes the matrix.
 */
export const PERMISSIONS = {
  // Admin module
  purge: 'CanPurge',
  manageRolePermissions: 'CanManageRolePermissions',

  // FSMS module
  viewTemplates: 'ViewTemplates',
  manageTemplates: 'ManageTemplates',
  viewSurveys: 'ViewSurveys',
  manageAssignments: 'ManageAssignments',
  createSurveys: 'CreateSurveys',
  submitSurveys: 'SubmitSurveys',
  reviewSurveys: 'ReviewSurveys',
  manageTeams: 'ManageTeams',
  manageLookups: 'ManageLookups',
  manageUsers: 'ManageUsers',
  viewDashboard: 'ViewDashboard',
  viewReports: 'ViewReports',
  importData: 'ImportData',
} as const;

/**
 * The super admin. Its access comes from a server-side bypass rather than from grants, so it holds
 * no permission claims of its own and has to be named alongside every permission a guard checks.
 * `AuthStore` loads role names into ngx-permissions next to the permission codes, which is what
 * lets a single `only: [...]` list mix the two.
 */
export const ADMINISTRATOR_ROLE = 'Administrator';

/** Role keys, matching KH.Domain.Constants.Fsms.FsmsRoles. */
export const ROLES = {
  administrator: ADMINISTRATOR_ROLE,
  supportOperations: 'SupportOperations',
  /** The back office — kept under its original key; "Back Office" is its display label. */
  dispatcher: 'Dispatcher',
  reviewer: 'Reviewer',
  monitor: 'Monitor',
  fieldTeam: 'FieldTeam',
} as const;

export type PermissionCode = (typeof PERMISSIONS)[keyof typeof PERMISSIONS];
export type RoleName = (typeof ROLES)[keyof typeof ROLES];
