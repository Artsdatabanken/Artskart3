import { TestBed } from '@angular/core/testing';
import { FilterStateService } from './filter-state.service';

describe('FilterStateService', () => {
  let service: FilterStateService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(FilterStateService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should start with empty selection', () => {
    expect(service.selectedCategoryIds()).toEqual([]);
    expect(service.selectedCountyIds()).toEqual([]);
    expect(service.selectedMunicipalityIds()).toEqual([]);
  });

  describe('toggleCategory', () => {
    it('should add category id when not selected', () => {
      service.toggleCategory(1);
      expect(service.selectedCategoryIds()).toEqual([1]);
    });

    it('should remove category id when already selected', () => {
      service.toggleCategory(1);
      service.toggleCategory(1);
      expect(service.selectedCategoryIds()).toEqual([]);
    });

    it('should handle multiple selections', () => {
      service.toggleCategory(1);
      service.toggleCategory(2);
      service.toggleCategory(3);
      expect(service.selectedCategoryIds()).toEqual([1, 2, 3]);
    });

    it('should remove only the toggled id', () => {
      service.toggleCategory(1);
      service.toggleCategory(2);
      service.toggleCategory(1);
      expect(service.selectedCategoryIds()).toEqual([2]);
    });
  });

  describe('clearCategories', () => {
    it('should reset selection to empty', () => {
      service.toggleCategory(1);
      service.toggleCategory(2);
      service.clearCategories();
      expect(service.selectedCategoryIds()).toEqual([]);
    });
  });

  describe('toggleMunicipality', () => {
    it('should add municipality fid when not selected', () => {
      service.toggleMunicipality('0301');
      expect(service.selectedMunicipalityIds()).toEqual(['0301']);
    });

    it('should remove municipality fid when already selected', () => {
      service.toggleMunicipality('0301');
      service.toggleMunicipality('0301');
      expect(service.selectedMunicipalityIds()).toEqual([]);
    });

    it('should handle multiple selections', () => {
      service.toggleMunicipality('0301');
      service.toggleMunicipality('0602');
      expect(service.selectedMunicipalityIds()).toEqual(['0301', '0602']);
    });
  });

  describe('toggleCounty', () => {
    it('should add county fid when not selected', () => {
      service.toggleCounty('03');
      expect(service.selectedCountyIds()).toEqual(['03']);
    });

    it('should remove county fid when already selected', () => {
      service.toggleCounty('03');
      service.toggleCounty('03');
      expect(service.selectedCountyIds()).toEqual([]);
    });
  });

  describe('addMunicipality / removeMunicipality', () => {
    it('should add without duplicates', () => {
      service.addMunicipality('0301');
      service.addMunicipality('0301');
      expect(service.selectedMunicipalityIds()).toEqual(['0301']);
    });

    it('should remove only the specified fid', () => {
      service.addMunicipality('0301');
      service.addMunicipality('0602');
      service.removeMunicipality('0301');
      expect(service.selectedMunicipalityIds()).toEqual(['0602']);
    });
  });

  describe('clearAreas', () => {
    it('should reset both county and municipality selections to empty', () => {
      service.addCounty('03');
      service.addMunicipality('0301');
      service.clearAreas();
      expect(service.selectedCountyIds()).toEqual([]);
      expect(service.selectedMunicipalityIds()).toEqual([]);
    });
  });

  describe('clearAll', () => {
    it('should clear categories, counties, municipalities, institutions, behaviors, and basisOfRecords', () => {
      service.toggleCategory(1);
      service.addCounty('03');
      service.addMunicipality('0301');
      service.addInstitution(5);
      service.addBehavior(10);
      service.addBasisOfRecord(7);
      service.clearAll();
      expect(service.selectedCategoryIds()).toEqual([]);
      expect(service.selectedCountyIds()).toEqual([]);
      expect(service.selectedMunicipalityIds()).toEqual([]);
      expect(service.selectedInstitutionIds()).toEqual([]);
      expect(service.selectedBehaviorIds()).toEqual([]);
      expect(service.selectedBasisOfRecordIds()).toEqual([]);
    });
  });

  describe('toggleInstitution', () => {
    it('should add institution id when not selected', () => {
      service.toggleInstitution(1);
      expect(service.selectedInstitutionIds()).toEqual([1]);
    });

    it('should remove institution id when already selected', () => {
      service.toggleInstitution(1);
      service.toggleInstitution(1);
      expect(service.selectedInstitutionIds()).toEqual([]);
    });

    it('should handle multiple selections', () => {
      service.toggleInstitution(1);
      service.toggleInstitution(2);
      expect(service.selectedInstitutionIds()).toEqual([1, 2]);
    });
  });

  describe('addInstitution / removeInstitution', () => {
    it('should add without duplicates', () => {
      service.addInstitution(1);
      service.addInstitution(1);
      expect(service.selectedInstitutionIds()).toEqual([1]);
    });

    it('should remove only the specified id', () => {
      service.addInstitution(1);
      service.addInstitution(2);
      service.removeInstitution(1);
      expect(service.selectedInstitutionIds()).toEqual([2]);
    });
  });

  describe('clearInstitutions', () => {
    it('should reset institution selection to empty', () => {
      service.addInstitution(1);
      service.addInstitution(2);
      service.clearInstitutions();
      expect(service.selectedInstitutionIds()).toEqual([]);
    });
  });

  describe('toggleBehavior', () => {
    it('should add behavior id when not selected', () => {
      service.toggleBehavior(1);
      expect(service.selectedBehaviorIds()).toEqual([1]);
    });

    it('should remove behavior id when already selected', () => {
      service.toggleBehavior(1);
      service.toggleBehavior(1);
      expect(service.selectedBehaviorIds()).toEqual([]);
    });

    it('should handle multiple selections', () => {
      service.toggleBehavior(1);
      service.toggleBehavior(2);
      expect(service.selectedBehaviorIds()).toEqual([1, 2]);
    });
  });

  describe('addBehavior / removeBehavior', () => {
    it('should add without duplicates', () => {
      service.addBehavior(1);
      service.addBehavior(1);
      expect(service.selectedBehaviorIds()).toEqual([1]);
    });

    it('should remove only the specified id', () => {
      service.addBehavior(1);
      service.addBehavior(2);
      service.removeBehavior(1);
      expect(service.selectedBehaviorIds()).toEqual([2]);
    });
  });

  describe('clearBehaviors', () => {
    it('should reset behavior selection to empty', () => {
      service.addBehavior(1);
      service.addBehavior(2);
      service.clearBehaviors();
      expect(service.selectedBehaviorIds()).toEqual([]);
    });
  });

  describe('toggleBasisOfRecord', () => {
    it('should add basisOfRecord id when not selected', () => {
      service.toggleBasisOfRecord(1);
      expect(service.selectedBasisOfRecordIds()).toEqual([1]);
    });

    it('should remove basisOfRecord id when already selected', () => {
      service.toggleBasisOfRecord(1);
      service.toggleBasisOfRecord(1);
      expect(service.selectedBasisOfRecordIds()).toEqual([]);
    });

    it('should handle multiple selections', () => {
      service.toggleBasisOfRecord(1);
      service.toggleBasisOfRecord(2);
      expect(service.selectedBasisOfRecordIds()).toEqual([1, 2]);
    });
  });

  describe('addBasisOfRecord / removeBasisOfRecord', () => {
    it('should add without duplicates', () => {
      service.addBasisOfRecord(1);
      service.addBasisOfRecord(1);
      expect(service.selectedBasisOfRecordIds()).toEqual([1]);
    });

    it('should remove only the specified id', () => {
      service.addBasisOfRecord(1);
      service.addBasisOfRecord(2);
      service.removeBasisOfRecord(1);
      expect(service.selectedBasisOfRecordIds()).toEqual([2]);
    });
  });

  describe('clearBasisOfRecords', () => {
    it('should reset basisOfRecord selection to empty', () => {
      service.addBasisOfRecord(1);
      service.addBasisOfRecord(2);
      service.clearBasisOfRecords();
      expect(service.selectedBasisOfRecordIds()).toEqual([]);
    });
  });

  describe('setPeriod', () => {
    it('should set period from and to values', () => {
      service.setPeriod(1900, 2020);
      expect(service.periodFrom()).toBe(1900);
      expect(service.periodTo()).toBe(2020);
    });

    it('should allow null values', () => {
      service.setPeriod(1900, null);
      expect(service.periodFrom()).toBe(1900);
      expect(service.periodTo()).toBeNull();
    });
  });

  describe('clearPeriod', () => {
    it('should reset period to null', () => {
      service.setPeriod(1900, 2020);
      service.clearPeriod();
      expect(service.periodFrom()).toBeNull();
      expect(service.periodTo()).toBeNull();
    });
  });

  describe('clearAll', () => {
    it('should also clear period', () => {
      service.setPeriod(1900, 2020);
      service.clearAll();
      expect(service.periodFrom()).toBeNull();
      expect(service.periodTo()).toBeNull();
    });
  });

  describe('toggleOceanArea', () => {
    it('should add ocean area fid when not selected', () => {
      service.toggleOceanArea('91');
      expect(service.selectedOceanAreaIds()).toEqual(['91']);
    });

    it('should remove ocean area fid when already selected', () => {
      service.toggleOceanArea('91');
      service.toggleOceanArea('91');
      expect(service.selectedOceanAreaIds()).toEqual([]);
    });

    it('should handle multiple selections', () => {
      service.toggleOceanArea('91');
      service.toggleOceanArea('92');
      service.toggleOceanArea('93');
      expect(service.selectedOceanAreaIds()).toEqual(['91', '92', '93']);
    });

    it('should remove only the toggled fid', () => {
      service.toggleOceanArea('91');
      service.toggleOceanArea('92');
      service.toggleOceanArea('91');
      expect(service.selectedOceanAreaIds()).toEqual(['92']);
    });
  });

  describe('clearOceanAreas', () => {
    it('should reset selection to empty', () => {
      service.toggleOceanArea('91');
      service.toggleOceanArea('92');
      service.clearOceanAreas();
      expect(service.selectedOceanAreaIds()).toEqual([]);
    });
  });

  describe('projects and datasets', () => {
    const kartlegging = { id: 14842, name: 'Kartlegging' };
    const overvaking = { id: 14843, name: 'Overvåking' };

    it('should start empty', () => {
      expect(service.selectedProjects()).toEqual([]);
      expect(service.selectedDatasets()).toEqual([]);
      expect(service.catalogNumber()).toBe('');
      expect(service.catalogObservationIds()).toEqual([]);
    });

    it('should add several projects and ignore one that is already selected', () => {
      service.addProject(kartlegging);
      service.addProject(overvaking);
      service.addProject({ ...kartlegging, name: 'Duplikat' });
      expect(service.selectedProjects()).toEqual([kartlegging, overvaking]);
      expect(service.selectedProjectIds()).toEqual([14842, 14843]);
    });

    it('should remove only the given project', () => {
      service.addProject(kartlegging);
      service.addProject(overvaking);
      service.removeProject(14842);
      expect(service.selectedProjects()).toEqual([overvaking]);
    });

    it('should rename a selected project without changing the ids', () => {
      service.addProject({ id: 14842, name: '' });
      const idsBefore = service.selectedProjectIds();
      service.setProjectName(14842, 'Kartlegging');
      expect(service.selectedProjects()).toEqual([kartlegging]);
      expect(service.selectedProjectIds()).toBe(idsBefore);
    });

    it('should not add a project when renaming one that is not selected', () => {
      service.setProjectName(14842, 'Kartlegging');
      expect(service.selectedProjects()).toEqual([]);
    });

    it('should add, rename and remove datasets the same way', () => {
      service.addDataset({ id: 26435, name: '' });
      service.addDataset({ id: 26436, name: 'Aqua Kompetanse AS' });
      service.setDatasetName(26435, 'Artsobservasjoner');
      service.removeDataset(26436);
      expect(service.selectedDatasets()).toEqual([{ id: 26435, name: 'Artsobservasjoner' }]);
      expect(service.selectedDatasetIds()).toEqual([26435]);
    });

    it('should set catalog number and observation ids independently', () => {
      service.setCatalogNumber('104168');
      expect(service.catalogObservationIds()).toEqual([]);

      service.setCatalogObservationIds([8368071, 8368072]);
      expect(service.catalogNumber()).toBe('104168');
      expect(service.catalogObservationIds()).toEqual([8368071, 8368072]);
    });
  });

  describe('clearOtherFindProperties', () => {
    it('should reset projects, datasets, catalog number and the image filter', () => {
      service.addProject({ id: 14842, name: 'Kartlegging' });
      service.addDataset({ id: 26435, name: 'Aqua Kompetanse AS' });
      service.setCatalogNumber('104168');
      service.setCatalogObservationIds([8368071]);
      service.setImageFilter('withImage');

      service.clearOtherFindProperties();

      expect(service.selectedProjects()).toEqual([]);
      expect(service.selectedDatasets()).toEqual([]);
      expect(service.catalogNumber()).toBe('');
      expect(service.catalogObservationIds()).toEqual([]);
      expect(service.imageFilter()).toBe('all');
    });
  });

  // clearAll nullstiller via clearOtherFindProperties. Glipper den koblingen,
  // blir «nullstill alle filtre» liggende igjen med et aktivt ID-filter som
  // ikke lenger vises noe sted.
  describe('clearAll', () => {
    it('should also clear projects, datasets and catalog number', () => {
      service.addProject({ id: 14842, name: 'Kartlegging' });
      service.addDataset({ id: 26435, name: 'Aqua Kompetanse AS' });
      service.setCatalogObservationIds([8368071]);

      service.clearAll();

      expect(service.selectedProjectIds()).toEqual([]);
      expect(service.selectedDatasetIds()).toEqual([]);
      expect(service.catalogObservationIds()).toEqual([]);
    });
  });
});
