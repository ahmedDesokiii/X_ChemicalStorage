
//// DataTables initialisation

$('#tableRole').dataTable({

    columnControl: {
        target: 1,
        content: ['search']
    },
    "autoWidth": true,
    "responsive": true,

    layout: {
        topEnd: {
            search: {
                placeholder: 'Search'
            }
        },
        topStart: {
            pageLength: {
                menu: [10, 25, 50, 100, 200, 400, 1000]
            }
        },
        bottomEnd: {
            paging: {
                buttons: 6
            }
        }
    },
    language: {
        "autoWidth": true,
        "responsive": true,
        search: '', // Customize the search input label
        lengthMenu: ' _MENU_ ', // Customize the "Show X entries" label
        info: 'Show _START_ To _END_ From _TOTAL_ entries', // Customize the info text
        zeroRecords: 'No records found', // No records message
        paginate: {
            first: 'Frist',
            previous: 'Next',
            next: 'Prev',
            last: 'Last'
        }
    }
});

$('#tablePermission').dataTable({

    columnControl: {
        target: 1,
        content: ['search']
    },
    "autoWidth": true,
    "responsive": true,
    "pageLength": 25,
    layout: {
        topEnd: {
            search: {
                placeholder: 'Search'
            }
        },
        topStart: {
            pageLength: {
                menu: [10, 25, 50, 100, 200, 400, 1000]
            }
        },
        bottomEnd: {
            paging: {
                buttons: 6
            }
        }
    },
    language: {
        "autoWidth": true,
        "responsive": true,
        search: '', // Customize the search input label
        lengthMenu: ' _MENU_ ', // Customize the "Show X entries" label
        info: 'Show _START_ To _END_ From _TOTAL_ entries', // Customize the info text
        zeroRecords: 'No records found', // No records message
        paginate: {
            first: 'Frist',
            previous: 'Next',
            next: 'Prev',
            last: 'Last'
        }
    }
});

function DeleteRole(id) {
    Swal.fire({
        title: lbTitleMsgDelete,
        text: lbTextMsgDelete,
        icon: 'error',
        showCancelButton: true,
        confirmButtonColor: '#d33',
        cancelButtonColor: '#57595B',
        confirmButtonText: lbconfirmButtonText,
        cancelButtonText: lbcancelButtonText,
        
    }).then((result) => {
        if (result.isConfirmed) {
            window.location.href = `/Roles/DeleteRole?Id=${id}`;
        }
    })
}

Edit = (id, name, det) => {

    document.getElementById("title").innerHTML = lbTitleEdit;
    document.getElementById("btnSave").value = lbEdit;
    document.getElementById("roleId").value = id;
    document.getElementById("roleName").value = name;
    document.getElementById("roleDetails").value = det;

}

ResetRole = () => {
    document.getElementById("title").innerHTML = lbAddNewRole;
    document.getElementById("btnSave").value = lbbtnSave;
    document.getElementById("roleId").value = "";
    document.getElementById("roleName").value = "";
    document.getElementById("roleDetails").value = "";

}

