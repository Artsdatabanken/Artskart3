export interface paths {
    "/api/Lookup/Categories": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["CategoryTypeDto"][];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/Lookup/Areas": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["AreaResponseDto"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/Lookup/Institutions": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["InstitutionDto"][];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/Lookup/TaxonGroups": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["TaxonGroupDto"][];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/Lookup/Behaviors": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["BehaviorDto"][];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/Lookup/BasisOfRecords": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["BasisOfRecordDto"][];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/Search/Observation": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["ObservationSearchFilterDto"];
                    "text/json": components["schemas"]["ObservationSearchFilterDto"];
                    "application/*+json": components["schemas"]["ObservationSearchFilterDto"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["PagedObservationResponseDto"];
                    };
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
}
export type webhooks = Record<string, never>;
export interface components {
    schemas: {
        CategoryTypeDto: {
            /** Format: int32 */
            id?: number;
            name?: string | null;
            categories?: components["schemas"]["CategoryDto"][] | null;
        };
        AreaResponseDto: {
            counties?: components["schemas"]["CountyDto"];
            municipalities?: components["schemas"]["AreaTypeDto"];
            restrictedAreas?: components["schemas"]["AreaTypeDto"];
            oceanAreas?: components["schemas"]["AreaTypeDto"];
        };
        InstitutionDto: {
            /** Format: int32 */
            id?: number;
            name?: string | null;
            code?: string | null;
            /** Format: int32 */
            observationCount?: number | null;
        };
        TaxonGroupDto: {
            /** Format: int32 */
            id?: number;
            name?: string | null;
            /** Format: int32 */
            observationCount?: number | null;
        };
        BehaviorDto: {
            /** Format: int32 */
            id?: number;
            name?: string | null;
            variants?: string | null;
            /** Format: int32 */
            observationCount?: number | null;
            description?: string | null;
        };
        BasisOfRecordDto: {
            /** Format: int32 */
            id?: number;
            name?: string | null;
            description?: string | null;
            variants?: string | null;
            /** Format: int32 */
            observationCount?: number | null;
        };
        ObservationSearchFilterDto: {
            /** Format: int32 */
            pageNumber?: number | null;
            /** Format: int32 */
            resultsPerPage?: number | null;
            readonly isPaginated?: boolean;
            preferredPopularName?: string | null;
            scientificName?: string | null;
            author?: string | null;
            taxonGroupIds?: number[] | null;
            categoryIds?: number[] | null;
            organizationIds?: number[] | null;
            municipalityIds?: string[] | null;
            countyIds?: string[] | null;
            behaviorIds?: number[] | null;
            basisOfRecordIds?: number[] | null;
            coordinatePrecision?: components["schemas"]["CoordinatePrecisionDto"];
            period?: components["schemas"]["PeriodDto"];
        };
        PagedObservationResponseDto: {
            items?: components["schemas"]["ObservationDto"][] | null;
            /** Format: int32 */
            pageNumber?: number;
            /** Format: int32 */
            resultsPerPage?: number;
            /** Format: int32 */
            lookaheadCount?: number;
        };
        CategoryDto: {
            /** Format: int32 */
            id?: number;
            code?: string | null;
            name?: string | null;
            /** Format: int32 */
            observationCount?: number | null;
        };
        CountyDto: {
            fastlandsNorge?: components["schemas"]["AreaDto"][] | null;
            janMayen?: components["schemas"]["AreaDto"];
            svalbard?: components["schemas"]["AreaDto"];
        };
        AreaTypeDto: {
            /** Format: int32 */
            id?: number;
            name?: string | null;
            areas?: components["schemas"]["AreaDto"][] | null;
        };
        CoordinatePrecisionDto: {
            /** Format: int32 */
            from?: number | null;
            /** Format: int32 */
            to?: number | null;
        };
        PeriodDto: {
            /** Format: int32 */
            from?: number | null;
            /** Format: int32 */
            to?: number | null;
        };
        ObservationDto: {
            /** Format: int32 */
            id?: number;
            preferredPopularName?: string | null;
            scientificName?: string | null;
            author?: string | null;
            institution?: string | null;
            locality?: string | null;
            municipalityId?: string | null;
            /** Format: int32 */
            taxonGroupId?: number | null;
            /** Format: int32 */
            categoryId?: number | null;
            /** Format: date-time */
            dateTimeCollected?: string | null;
            /** Format: int32 */
            coordinatePrecisionInMeters?: number | null;
        };
        AreaDto: {
            /** Format: int32 */
            id?: number;
            fid?: string | null;
            name?: string | null;
            isCurrent?: boolean;
            /** Format: int32 */
            observationCount?: number | null;
        };
    };
    responses: never;
    parameters: never;
    requestBodies: never;
    headers: never;
    pathItems: never;
}
export type $defs = Record<string, never>;
export type operations = Record<string, never>;
