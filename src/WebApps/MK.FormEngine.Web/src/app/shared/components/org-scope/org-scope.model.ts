import { OrgScopeAssignment } from '../../../core/api/api-client.generated';

/**
 * The scope levels as the API names them (`KH.Domain.Constants.Fsms.OrgScopeLevels`):
 *
 * ```
 * Cluster
 *   └── Cbu
 *        ├── Branch
 *        └── OperationArea
 * ```
 *
 * Branch and operation area are siblings under the CBU, not a chain, so a level's position in
 * {@link LEVEL_ORDER} says nothing about coverage — {@link isLevelUnder} is what decides that.
 */
export const ORG_SCOPE_LEVELS = {
  cluster: 'Cluster',
  cbu: 'Cbu',
  branch: 'Branch',
  operationArea: 'OperationArea',
} as const;

export type OrgScopeLevel = (typeof ORG_SCOPE_LEVELS)[keyof typeof ORG_SCOPE_LEVELS];

/** Display order, broadest first. Not a coverage ranking — see {@link isLevelUnder}. */
export const LEVEL_ORDER: readonly OrgScopeLevel[] = [
  ORG_SCOPE_LEVELS.cluster,
  ORG_SCOPE_LEVELS.cbu,
  ORG_SCOPE_LEVELS.branch,
  ORG_SCOPE_LEVELS.operationArea,
];

/** Each level's parent. Mirrors `OrgScopeLevels.ParentOf` on the API. */
const LEVEL_PARENT: Readonly<Partial<Record<OrgScopeLevel, OrgScopeLevel>>> = {
  [ORG_SCOPE_LEVELS.cbu]: ORG_SCOPE_LEVELS.cluster,
  [ORG_SCOPE_LEVELS.branch]: ORG_SCOPE_LEVELS.cbu,
  [ORG_SCOPE_LEVELS.operationArea]: ORG_SCOPE_LEVELS.cbu,
};

/**
 * True when `level` sits somewhere beneath `ancestor`. Two siblings — branch and operation area —
 * answer `false` in both directions, which is why this exists instead of comparing positions in
 * {@link LEVEL_ORDER}.
 */
export function isLevelUnder(level: OrgScopeLevel, ancestor: OrgScopeLevel): boolean {
  for (let current = LEVEL_PARENT[level]; current; current = LEVEL_PARENT[current]) {
    if (current === ancestor) {
      return true;
    }
  }

  return false;
}

/** Translation key per level, so a caller never hardcodes a label. */
export const LEVEL_LABEL_KEYS: Readonly<Record<OrgScopeLevel, string>> = {
  [ORG_SCOPE_LEVELS.cluster]: 'org.cluster',
  [ORG_SCOPE_LEVELS.cbu]: 'org.cbu',
  [ORG_SCOPE_LEVELS.branch]: 'org.branch',
  [ORG_SCOPE_LEVELS.operationArea]: 'org.operationArea',
};

/**
 * One place in the geography. The cluster is carried so the picker can restore its own cascade when
 * editing, but only the three narrower codes are stored on a survey — the cluster is always
 * derivable from the CBU.
 *
 * Note this is the *single*-mode value. A multi-mode row is an {@link OrgScopeAssignment}, which
 * pairs one level/code with a department and allows either half to be absent.
 */
export interface OrgLocation {
  clusterCode: string | null;
  cbuCode: string | null;
  branchCode: string | null;
  operationAreaCode: string | null;
}

export const EMPTY_ORG_LOCATION: OrgLocation = {
  clusterCode: null,
  cbuCode: null,
  branchCode: null,
  operationAreaCode: null,
};

/** True when nothing at all has been picked. */
export function isEmptyLocation(location: OrgLocation | null | undefined): boolean {
  return (
    !location?.clusterCode && !location?.cbuCode && !location?.branchCode && !location?.operationAreaCode
  );
}

export type { OrgScopeAssignment };
