import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, shareReplay, throwError } from 'rxjs';

import {
  FsmsCbuDto,
  FsmsClusterDto,
  FsmsContractorDto,
  FsmsCustomerTypeDto,
  FsmsDepartmentDto,
  FsmsFaTypeDto,
  FsmsLookupsClient,
  FsmsOperationAreaDto,
  FsmsReturnReasonDto,
  FsmsTaskTypeDto,
  FsmsBranchDto,
  FsmsTeamsClient,
  TeamDto,
} from '../api/api-client.generated';

/**
 * Reference data is small and slow-moving, so each list is fetched once per browser session and
 * replayed to every subscriber. The API caches the same lists server-side and evicts them whenever
 * a lookup row is added or edited; call `clear()` after any client-side lookup mutation.
 *
 * The org geography (cluster → CBU → branch | operation area) is fetched per parent rather than
 * whole, because that is how the cascading pickers read it: pick a cluster, ask for its CBUs. A
 * single cached observable cannot serve two different parents, so those levels cache into a map
 * keyed by parent code — with `''` standing for "no parent filter", i.e. the whole level.
 */
@Injectable({ providedIn: 'root' })
export class FsmsLookupService {
  /** Reference tables are far smaller than this — one page is always the whole list. */
  private static readonly PAGE_SIZE = 1000;
  private static readonly FIRST_PAGE = 1;

  /** Map key for an unfiltered level; a real parent code is never empty. */
  private static readonly NO_PARENT = '';

  private readonly lookupsClient = inject(FsmsLookupsClient);
  private readonly teamsClient = inject(FsmsTeamsClient);

  private departments$?: Observable<FsmsDepartmentDto[]>;
  private faTypes$?: Observable<FsmsFaTypeDto[]>;
  private taskTypes$?: Observable<FsmsTaskTypeDto[]>;
  private customerTypes$?: Observable<FsmsCustomerTypeDto[]>;
  private contractors$?: Observable<FsmsContractorDto[]>;
  private returnReasons$?: Observable<FsmsReturnReasonDto[]>;
  private clusters$?: Observable<FsmsClusterDto[]>;
  private teams$?: Observable<TeamDto[]>;

  private readonly cbusByCluster = new Map<string, Observable<FsmsCbuDto[]>>();
  private readonly branchesByCbu = new Map<string, Observable<FsmsBranchDto[]>>();
  private readonly operationAreasByCbu = new Map<string, Observable<FsmsOperationAreaDto[]>>();

  /** Active departments, cached for the session. */
  getDepartments(): Observable<FsmsDepartmentDto[]> {
    this.departments$ ??= this.lookupsClient
      .fsmsLookups_GetDepartments(FsmsLookupService.FIRST_PAGE, FsmsLookupService.PAGE_SIZE, undefined, true)
      .pipe(
        map(res => res?.data?.items ?? []),
        // A cached failure would stick forever — drop it so the next caller retries.
        catchError(error => {
          this.departments$ = undefined;
          return throwError(() => error);
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );

    return this.departments$;
  }

  /** Active FA types / task types, cached for the session. */
  getFaTypes(): Observable<FsmsFaTypeDto[]> {
    this.faTypes$ ??= this.lookupsClient
      .fsmsLookups_GetFaTypes(FsmsLookupService.FIRST_PAGE, FsmsLookupService.PAGE_SIZE, undefined, true)
      .pipe(
        map(res => res?.data?.items ?? []),
        catchError(error => {
          this.faTypes$ = undefined;
          return throwError(() => error);
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );

    return this.faTypes$;
  }

  /** Active task types, cached for the session. */
  getTaskTypes(): Observable<FsmsTaskTypeDto[]> {
    this.taskTypes$ ??= this.lookupsClient
      .fsmsLookups_GetTaskTypes(FsmsLookupService.FIRST_PAGE, FsmsLookupService.PAGE_SIZE, undefined, true)
      .pipe(
        map(res => res?.data?.items ?? []),
        catchError(error => {
          this.taskTypes$ = undefined;
          return throwError(() => error);
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );

    return this.taskTypes$;
  }

  /** Active customer types, cached for the session. */
  getCustomerTypes(): Observable<FsmsCustomerTypeDto[]> {
    this.customerTypes$ ??= this.lookupsClient
      .fsmsLookups_GetCustomerTypes(FsmsLookupService.FIRST_PAGE, FsmsLookupService.PAGE_SIZE, undefined, true)
      .pipe(
        map(res => res?.data?.items ?? []),
        catchError(error => {
          this.customerTypes$ = undefined;
          return throwError(() => error);
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );

    return this.customerTypes$;
  }

  /** Active contractors, cached for the session. */
  getContractors(): Observable<FsmsContractorDto[]> {
    this.contractors$ ??= this.lookupsClient
      .fsmsLookups_GetContractors(FsmsLookupService.FIRST_PAGE, FsmsLookupService.PAGE_SIZE, undefined, true)
      .pipe(
        map(res => res?.data?.items ?? []),
        catchError(error => {
          this.contractors$ = undefined;
          return throwError(() => error);
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );

    return this.contractors$;
  }

  /** Active survey return reasons, cached for the session. Already sorted by the API. */
  getReturnReasons(): Observable<FsmsReturnReasonDto[]> {
    this.returnReasons$ ??= this.lookupsClient
      .fsmsLookups_GetReturnReasons(FsmsLookupService.FIRST_PAGE, FsmsLookupService.PAGE_SIZE, undefined, true)
      .pipe(
        map(res => res?.data?.items ?? []),
        catchError(error => {
          this.returnReasons$ = undefined;
          return throwError(() => error);
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );

    return this.returnReasons$;
  }

  /** Active clusters — the top of the org geography. */
  getClusters(): Observable<FsmsClusterDto[]> {
    this.clusters$ ??= this.lookupsClient
      .fsmsLookups_GetClusters(FsmsLookupService.FIRST_PAGE, FsmsLookupService.PAGE_SIZE, undefined, true)
      .pipe(
        map(res => res?.data?.items ?? []),
        catchError(error => {
          this.clusters$ = undefined;
          return throwError(() => error);
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );

    return this.clusters$;
  }

  /** Active CBUs, optionally narrowed to one cluster. */
  getCbus(clusterCode?: string | null): Observable<FsmsCbuDto[]> {
    return this.parented(this.cbusByCluster, clusterCode, parent =>
      this.lookupsClient.fsmsLookups_GetCbus(
        FsmsLookupService.FIRST_PAGE,
        FsmsLookupService.PAGE_SIZE,
        undefined,
        true,
        parent,
      ),
    );
  }

  /** Active branches, optionally narrowed to one CBU. */
  getBranches(cbuCode?: string | null): Observable<FsmsBranchDto[]> {
    return this.parented(this.branchesByCbu, cbuCode, parent =>
      this.lookupsClient.fsmsLookups_GetBranches(
        FsmsLookupService.FIRST_PAGE,
        FsmsLookupService.PAGE_SIZE,
        undefined,
        true,
        parent,
      ),
    );
  }

  /** Active operation areas, optionally narrowed to one CBU — they hang off the CBU, not a branch. */
  getOperationAreas(cbuCode?: string | null): Observable<FsmsOperationAreaDto[]> {
    return this.parented(this.operationAreasByCbu, cbuCode, parent =>
      this.lookupsClient.fsmsLookups_GetOperationAreas(
        FsmsLookupService.FIRST_PAGE,
        FsmsLookupService.PAGE_SIZE,
        undefined,
        true,
        parent,
      ),
    );
  }

  /** Active teams — cached for the session; Reports' team filter/options reuse this same list. */
  getTeams(): Observable<TeamDto[]> {
    this.teams$ ??= this.teamsClient
      .fsmsTeams_GetTeamsPaged(FsmsLookupService.FIRST_PAGE, FsmsLookupService.PAGE_SIZE, undefined, true)
      .pipe(
        map(res => res?.data?.items ?? []),
        catchError(error => {
          this.teams$ = undefined;
          return throwError(() => error);
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );

    return this.teams$;
  }

  /** Drops every cached list so the next read hits the API again. */
  clear(): void {
    this.departments$ = undefined;
    this.faTypes$ = undefined;
    this.taskTypes$ = undefined;
    this.customerTypes$ = undefined;
    this.contractors$ = undefined;
    this.returnReasons$ = undefined;
    this.clusters$ = undefined;
    this.teams$ = undefined;
    this.cbusByCluster.clear();
    this.branchesByCbu.clear();
    this.operationAreasByCbu.clear();
  }

  /**
   * Shared caching for a level of the geography that is read per parent. Each distinct parent gets
   * its own replayed observable; a failure evicts only that parent's entry, so one bad cluster does
   * not poison the rest of the map.
   */
  private parented<TItem, TResponse extends { data?: { items?: TItem[] | undefined } }>(
    cache: Map<string, Observable<TItem[]>>,
    parentCode: string | null | undefined,
    request: (parent: string | undefined) => Observable<TResponse>,
  ): Observable<TItem[]> {
    const key = parentCode?.trim() || FsmsLookupService.NO_PARENT;
    const cached = cache.get(key);

    if (cached) {
      return cached;
    }

    const stream = request(key === FsmsLookupService.NO_PARENT ? undefined : key).pipe(
      map(res => res?.data?.items ?? []),
      catchError(error => {
        cache.delete(key);
        return throwError(() => error);
      }),
      shareReplay({ bufferSize: 1, refCount: false }),
    );

    cache.set(key, stream);

    return stream;
  }
}
