use tempfile::NamedTempFile;
use std::ptr::NonNull;
use std::str::FromStr;
use std::env;
use wasmer::{
    vm::{self, MemoryError, MemoryStyle, TableStyle, VMMemoryDefinition, VMTableDefinition},
    BaseTunables, Cranelift, CpuFeature, MemoryType, Module, Pages, Store, TableType, Target, Tunables
};

// LimitingTunablesの実装は https://github.com/wasmerio/wasmer/blob/master/examples/tunables_limit_memory.rs より借用
// https://github.com/wasmerio/wasmer/blob/master/LICENSE
pub struct LimitingTunables<T: Tunables> {
    limit: Pages,
    base: T,
}

impl<T: Tunables> LimitingTunables<T> {
    pub fn new(base: T, limit: Pages) -> Self {
        Self { limit, base }
    }

    fn adjust_memory(&self, requested: &MemoryType) -> MemoryType {
        let mut adjusted = requested.clone();
        if requested.maximum.is_none() {
            adjusted.maximum = Some(self.limit);
        }
        adjusted
    }

    fn validate_memory(&self, ty: &MemoryType) -> Result<(), MemoryError> {
        if ty.minimum > self.limit {
            return Err(MemoryError::Generic(
                "Minimum exceeds the allowed memory limit".to_string(),
            ));
        }

        if let Some(max) = ty.maximum {
            if max > self.limit {
                return Err(MemoryError::Generic(
                    "Maximum exceeds the allowed memory limit".to_string(),
                ));
            }
        } else {
            return Err(MemoryError::Generic("Maximum unset".to_string()));
        }

        Ok(())
    }
}

impl<T: Tunables> Tunables for LimitingTunables<T> {
    fn memory_style(&self, memory: &MemoryType) -> MemoryStyle {
        let adjusted = self.adjust_memory(memory);
        self.base.memory_style(&adjusted)
    }

    fn table_style(&self, table: &TableType) -> TableStyle {
        self.base.table_style(table)
    }

    fn create_host_memory(
        &self,
        ty: &MemoryType,
        style: &MemoryStyle,
    ) -> Result<vm::VMMemory, MemoryError> {
        let adjusted = self.adjust_memory(ty);
        self.validate_memory(&adjusted)?;
        self.base.create_host_memory(&adjusted, style)
    }

    unsafe fn create_vm_memory(
        &self,
        ty: &MemoryType,
        style: &MemoryStyle,
        vm_definition_location: NonNull<VMMemoryDefinition>,
    ) -> Result<vm::VMMemory, MemoryError> {
        let adjusted = self.adjust_memory(ty);
        self.validate_memory(&adjusted)?;
        self.base
            .create_vm_memory(&adjusted, style, vm_definition_location)
    }

    fn create_host_table(&self, ty: &TableType, style: &TableStyle) -> Result<vm::VMTable, String> {
        self.base.create_host_table(ty, style)
    }

    unsafe fn create_vm_table(
        &self,
        ty: &TableType,
        style: &TableStyle,
        vm_definition_location: NonNull<VMTableDefinition>,
    ) -> Result<vm::VMTable, String> {
        self.base.create_vm_table(ty, style, vm_definition_location)
    }
}

fn compile(source_path: &str, dest_path: &str, target: Target) {
    let source_bytes = std::fs::read(&source_path).unwrap();
    let compiler = Cranelift::default();

    let base = BaseTunables::for_target(&target);
    // Dirty hack (Force MemoryStyle::Dynamic)
    let tunables = LimitingTunables::new(base, Pages(wasmer::WASM_MAX_PAGES + 1));

    let store = Store::new_with_tunables(compiler, tunables);
    let module = Module::new(&store, source_bytes).unwrap();
        
    let serialized_module_file = NamedTempFile::new().unwrap();
    module.serialize_to_file(&serialized_module_file).unwrap();
    serialized_module_file.persist(dest_path).unwrap();
}

fn main() {
    let current_dir = env::current_dir().unwrap();
    let args: Vec<String> = env::args().collect();
    let source_path = format!("{}/{}", current_dir.to_str().unwrap(), &args[1]);
    let dest_path_prefix = format!("{}/{}", current_dir.to_str().unwrap(), &args[2]);
    // for M1 Mac
    compile(
        &source_path,
        &format!("{}.arm64.wasmu", dest_path_prefix),
        Target::new(wasmer::Triple::from_str("aarch64-apple-darwin").unwrap(),
        CpuFeature::set()
    ));
    // for Android (arm64)
    compile(
        &source_path,
        &format!("{}.android.wasmu", dest_path_prefix),
        Target::new(wasmer::Triple::from_str("aarch64-linux-android").unwrap(),
        CpuFeature::set()
    ));
    // for iOS
    compile(
        &source_path,
        &format!("{}.ios.wasmu", dest_path_prefix),
        Target::new(wasmer::Triple::from_str("aarch64-apple-ios").unwrap(),
        CpuFeature::set()
    ));
}
